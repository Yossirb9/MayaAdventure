"""Paints Maya's facial expressions and packs them for Maya/Character.

usage: python make_faces.py <texture_0.png> <projection_dir> <out_atlas.png> <out_FaceAtlasData.cs> [debug_dir]

<projection_dir> comes from face_projection.py (Blender): front.png (the face seen straight on) and texels.npz
(which texture texel shows at which front-image pixel). The expressions are painted on the straight front view,
where the face is upright and symmetric, then pushed back into texture space through texels.npz - so they stay
straight on the 3D face even though the face is split over several UV islands.

The atlas holds one tile per expression (top to bottom: normal, blink, happy, hurt, surprised). A tile is the
changed texture rects side by side; RGB = colour, A = how much of it replaces the original texture (the shader also
flattens the normal map there, so sculpted eyeballs don't show through closed lids). FaceAtlasData.cs tells the shader where the rects are.
"""
import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

# Features in the 1024x1024 front view (face_projection.py framing).
EYES = [  # centre, half width, half height (lashes included)
    dict(c=(275, 368), hw=112, hh=66, inner=+1),   # her right eye (left in the picture); nose is to +x
    dict(c=(668, 346), hw=118, hh=66, inner=-1),   # her left eye
]
MOUTH = dict(c=(482, 652), hw=150, hh=52)
LASH = (62, 36, 28)
DARK = (110, 26, 40)
TEETH = (252, 248, 242)
TONGUE = (226, 96, 108)
NAMES = ["Normal", "Blink", "Happy", "Hurt", "Surprised"]


def ellipse_mask(size, c, rx, ry, feather):
    m = Image.new('L', size, 0)
    ImageDraw.Draw(m).ellipse([c[0] - rx, c[1] - ry, c[0] + rx, c[1] + ry], fill=255)
    return m.filter(ImageFilter.GaussianBlur(feather))


def skin_fill(img, mask, sample_box):
    """Covers the masked area with skin: the average skin colour around it, softly blended."""
    a = np.asarray(img).astype(float)
    x0, y0, x1, y1 = sample_box
    region = a[y0:y1, x0:x1].reshape(-1, 3)
    ok = region[(region[:, 0] > 200) & (region[:, 1] > 120) & (region[:, 1] < 200) & (region[:, 2] < 170)]
    col = tuple(int(v) for v in np.median(ok, 0))
    return Image.composite(Image.new('RGB', img.size, col), img, mask)


def curve(draw, pts, width, colour):
    draw.line(pts, fill=colour, width=width, joint='curve')
    for p in (pts[0], pts[-1]):
        r = width / 2
        draw.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=colour)


def arc_pts(c, hw, bend, n=28, dy=0):
    """Points from left to right; bend > 0 bulges downwards (relaxed lid), < 0 upwards (happy ^)."""
    return [(c[0] + hw * t, c[1] + dy + bend * (1 - t * t)) for t in (-1 + 2 * i / n for i in range(n + 1))]


def maxi(a, b):
    return Image.fromarray(np.maximum(np.asarray(a), np.asarray(b)))


def paint(front, kind):
    img = front.copy()
    eye_mask = Image.new('L', img.size, 0)
    changed = Image.new('L', img.size, 0)
    if kind in ("Blink", "Happy", "Hurt"):
        for e in EYES:
            c, hw, hh = e['c'], e['hw'], e['hh']
            # wide enough to swallow the winged eyeliner at the outer corner too
            m = ellipse_mask(img.size, (c[0] - e['inner'] * 12, c[1] - 14), hw * 1.27, hh * 1.55, 7)
            img = skin_fill(img, m, (c[0] - hw - 40, c[1] + hh + 10, c[0] + hw + 40, c[1] + hh + 70))
            eye_mask = maxi(eye_mask, ellipse_mask(img.size, (c[0] - e['inner'] * 10, c[1] - 8), hw * 1.2, hh * 1.35, 12))
            changed = maxi(changed, m)
        d = ImageDraw.Draw(img)
        for e in EYES:
            c, hw, hh = e['c'], e['hw'], e['hh']
            if kind == "Blink":
                pts = arc_pts(c, hw * 0.92, hh * 0.32, dy=hh * 0.15)
                curve(d, pts, 11, LASH)
                # a few lashes at the outer corner
                outer = pts[0] if e['inner'] > 0 else pts[-1]
                for k in range(3):
                    ang = math.radians(200 + k * 18) if e['inner'] > 0 else math.radians(-20 - k * 18)
                    d.line([outer, (outer[0] + math.cos(ang) * 26, outer[1] + math.sin(ang) * 26)], fill=LASH, width=6)
            elif kind == "Happy":
                curve(d, arc_pts(c, hw * 0.78, -hh * 0.7, dy=hh * 0.3), 13, LASH)
            else:  # Hurt: squeezed shut, > <
                tip = (c[0] + e['inner'] * hw * 0.55, c[1])
                back = c[0] - e['inner'] * hw * 0.7
                curve(d, [(back, c[1] - hh * 0.55), tip, (back, c[1] + hh * 0.55)], 13, LASH)
    if kind == "Surprised":
        # eyes a little bigger: scale the eye up around its centre
        for e in EYES:
            c, hw, hh = e['c'], e['hw'], e['hh']
            box = (int(c[0] - hw * 1.1), int(c[1] - hh * 1.3), int(c[0] + hw * 1.1), int(c[1] + hh * 1.3))
            patch = img.crop(box)
            s = 1.16
            big = patch.resize((int(patch.width * s), int(patch.height * s)), Image.BICUBIC)
            m = ellipse_mask(big.size, (big.width / 2, big.height / 2), hw * s * 1.0, hh * s * 1.15, 5)
            off = (int(c[0] - big.width / 2), int(c[1] - big.height / 2))
            img.paste(big, off, m)
            full = Image.new('L', img.size, 0)
            full.paste(m, off)
            changed = maxi(changed, full)
    if kind in ("Happy", "Hurt", "Surprised"):
        c, hw, hh = MOUTH['c'], MOUTH['hw'], MOUTH['hh']
        m = ellipse_mask(img.size, c, hw * 1.12, hh * 1.25, 6)
        img = skin_fill(img, m, (c[0] - 90, c[1] + hh + 15, c[0] + 90, c[1] + hh + 60))
        changed = maxi(changed, m)
        d = ImageDraw.Draw(img)
        cx, cy = c
        if kind == "Happy":
            # open grin: the top edge smiles (corners up), the bottom is a deep curve
            top = [(cx + hw * t, cy - 14 - 26 * (t * t)) for t in np.linspace(-1, 1, 30)]
            bottom = [(cx + hw * t * 0.97, cy - 14 - 26 * (t * t) + 78 * (1 - t * t) ** 0.8) for t in np.linspace(1, -1, 30)]
            d.polygon(top + bottom, fill=DARK)
            teeth = [(cx + hw * 0.8 * t, cy - 12 - 22 * (t * t)) for t in np.linspace(-1, 1, 24)] + \
                    [(cx + hw * 0.8 * t, cy + 8 - 16 * (t * t)) for t in np.linspace(1, -1, 24)]
            d.polygon(teeth, fill=TEETH)
            d.ellipse([cx - 52, cy + 26, cx + 52, cy + 62], fill=TONGUE)
            curve(d, top, 4, (160, 60, 70))
        elif kind == "Hurt":
            d.rounded_rectangle([cx - 82, cy - 20, cx + 82, cy + 20], radius=18, fill=TEETH, outline=(150, 60, 70), width=5)
            d.line([(cx - 76, cy), (cx + 76, cy)], fill=(200, 185, 180), width=3)
            for k in (-40, 0, 40):
                d.line([(cx + k, cy - 18), (cx + k, cy + 18)], fill=(215, 200, 195), width=2)
        else:
            d.ellipse([cx - 36, cy - 30, cx + 36, cy + 42], fill=DARK, outline=(170, 70, 80), width=4)
    return img, changed, eye_mask


def fill_gutters(rgb, valid, steps=12):
    """Grows the colours of the texture islands into the empty gaps between them, so filtering at the
    island edges never picks up a stray colour (the thin dotted lines along seams)."""
    col, v = rgb.copy(), valid.copy()
    for _ in range(steps):
        acc = np.zeros_like(col)
        cnt = np.zeros(v.shape, np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dy or dx:
                    sv = np.roll(np.roll(v, dy, 0), dx, 1)
                    acc += np.roll(np.roll(col, dy, 0), dx, 1) * sv[..., None]
                    cnt += sv
        grow = (~v) & (cnt > 0)
        col[grow] = acc[grow] / cnt[grow][:, None]
        v |= grow
    return col


def main():
    tex_path, proj, out_atlas, out_cs = sys.argv[1:5]
    dbg = sys.argv[5] if len(sys.argv) > 5 else None
    tex = np.asarray(Image.open(tex_path).convert('RGB')).astype(float)
    H, W, _ = tex.shape
    # The islands of the original (undilated) texture: everything else is gap.
    import os
    orig = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'texture_0_original.png')
    valid = np.asarray(Image.open(orig).convert('RGB')).astype(int).sum(2) > 18
    front = Image.open(proj + '/front.png').convert('RGBA')
    bg = Image.new('RGBA', front.size, (255, 255, 255, 255))
    bg.alpha_composite(front)
    front = bg.convert('RGB')
    d = np.load(proj + '/texels.npz')
    tx, ty, fx, fy, facing = d['tx'], d['ty'], d['fx'], d['fy'], d['facing']
    # Fade out where the surface turns away from the camera (the paint would smear there).
    face_w = np.clip((facing - 0.05) / 0.2, 0, 1)

    def sample(img):
        a = np.asarray(img).astype(float)
        if a.ndim == 2:
            a = a[:, :, None]
        x = np.clip(fx, 0, a.shape[1] - 1.001)
        y = np.clip(fy, 0, a.shape[0] - 1.001)
        x0, y0 = x.astype(int), y.astype(int)
        ax, ay = (x - x0)[:, None], (y - y0)[:, None]
        return (a[y0, x0] * (1 - ax) * (1 - ay) + a[y0, x0 + 1] * ax * (1 - ay) +
                a[y0 + 1, x0] * (1 - ax) * ay + a[y0 + 1, x0 + 1] * ax * ay)

    variants = []  # (rgb texture, how much of it replaces the original: 0..1)
    union = np.zeros((H, W), bool)
    for kind in NAMES:
        rgb = tex.copy()
        alpha = np.zeros((H, W))
        if kind != "Normal":
            painted, changed, eyes = paint(front, kind)
            m = sample(changed)[:, 0] / 255 * face_w
            rgb[ty, tx] = sample(painted)
            alpha[ty, tx] = m
            # Spread colour and mask into the gaps between islands so filtering at seams stays clean.
            both = fill_gutters(np.where(valid[..., None], np.dstack([rgb, alpha[..., None] * 255]), 0), valid)
            rgb, alpha = both[..., :3], both[..., 3] / 255
            hit = m > 0.01
            union[ty[hit], tx[hit]] = True
            if dbg:
                painted.save(f'{dbg}/front_{kind}.png')
        variants.append((rgb, alpha))

    # Group the changed texels into a few rectangles (texture islands), padded for filtering.
    cell = 16
    gh, gw = H // cell, W // cell
    grid = union[:gh * cell, :gw * cell].reshape(gh, cell, gw, cell).any(axis=(1, 3))
    seen = np.zeros_like(grid)
    rects = []
    for gy in range(gh):
        for gx in range(gw):
            if not grid[gy, gx] or seen[gy, gx]:
                continue
            stack, cells = [(gy, gx)], []
            seen[gy, gx] = True
            while stack:
                y, x = stack.pop()
                cells.append((y, x))
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        ny, nx = y + dy, x + dx
                        if 0 <= ny < gh and 0 <= nx < gw and grid[ny, nx] and not seen[ny, nx]:
                            seen[ny, nx] = True
                            stack.append((ny, nx))
            ys = [c[0] for c in cells]
            xs = [c[1] for c in cells]
            rects.append([min(xs) * cell - 8, min(ys) * cell - 8, (max(xs) + 1) * cell + 8, (max(ys) + 1) * cell + 8])
    # Merge overlapping rectangles until stable.
    merged = True
    while merged:
        merged = False
        for i in range(len(rects)):
            for j in range(i + 1, len(rects)):
                a, b = rects[i], rects[j]
                if a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]:
                    rects[i] = [min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3])]
                    rects.pop(j)
                    merged = True
                    break
            if merged:
                break
    rects = [[max(0, r[0]), max(0, r[1]), min(W, r[2]), min(H, r[3])] for r in rects]
    rects.sort(key=lambda r: -(r[2] - r[0]) * (r[3] - r[1]))
    if len(rects) > 8:
        print('WARNING: more than 8 rects, dropping the smallest', rects[8:])
        rects = rects[:8]
    print('rects', rects)

    tile_w = sum(r[2] - r[0] for r in rects)
    tile_h = max(r[3] - r[1] for r in rects)
    atlas = np.zeros((tile_h * len(variants), tile_w, 4), np.uint8)
    for i, (rgb, alpha) in enumerate(variants):
        x = 0
        for r in rects:
            w, h = r[2] - r[0], r[3] - r[1]
            atlas[i * tile_h:i * tile_h + h, x:x + w, :3] = np.clip(rgb[r[1]:r[3], r[0]:r[2]], 0, 255)
            atlas[i * tile_h:i * tile_h + h, x:x + w, 3] = np.clip(alpha[r[1]:r[3], r[0]:r[2]] * 255, 0, 255)
            x += w
    Image.fromarray(atlas, 'RGBA').save(out_atlas)

    # C#: texture rects (uv min/max, v up) and where each sits inside a tile (tile uv min/max, v up).
    lines = []
    x = 0
    for r in rects:
        w, h = r[2] - r[0], r[3] - r[1]
        uv = (r[0] / W, 1 - r[3] / H, r[2] / W, 1 - r[1] / H)
        tile = (x / tile_w, 1 - h / tile_h, (x + w) / tile_w, 1.0)  # top-aligned in the tile
        lines.append(f"            new Vector4({uv[0]:.6f}f, {uv[1]:.6f}f, {uv[2]:.6f}f, {uv[3]:.6f}f), new Vector4({tile[0]:.6f}f, {tile[1]:.6f}f, {tile[2]:.6f}f, {tile[3]:.6f}f),")
        x += w
    while len(lines) < 8:
        lines.append("            Vector4.zero, Vector4.zero,")
    nl = "\n"
    cs = f'''using UnityEngine;

namespace MayaAdventure
{{
    /// <summary>Generated by Tools~/make_faces.py: where Maya's expression atlas sits on her texture.</summary>
    public static class FaceAtlasData
    {{
        public const int Tiles = {len(variants)};

        /// <summary>Pairs of (texture rect: u min, v min, u max, v max) and (its place in an atlas tile, same order).</summary>
        public static readonly Vector4[] Rects =
        {{
{nl.join(lines)}
        }};
    }}
}}
'''
    open(out_cs, 'w', encoding='utf-8').write(cs)
    if dbg:
        for i, (rgb, alpha) in enumerate(variants):
            Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).save(f'{dbg}/tex_expr{i}.png')


if __name__ == '__main__':
    main()
