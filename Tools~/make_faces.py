"""Paints Maya's facial expressions (blink, happy, hurt, surprised) into a small atlas that Maya/Character overlays
on the face part of her texture.

usage: python make_faces.py <texture_0.png> <out_atlas.png> [debug_dir]
The atlas holds one tile per expression (top to bottom: normal, blink, happy, hurt, surprised); every tile is the
RECT region of the texture, repainted. RECT must match _FaceRect set in MayaModel.
"""
import sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

RECT = (380, 1480, 780, 1720)            # x0, y0, x1, y1 in texture pixels (2048 texture)
RECT2 = (1340, 80, 1460, 200)            # a small separate patch holding the inner corner of her right eye
SLIVER = (1397, 128)                     # that corner's centre
EYES = [(640, 1567), (507, 1630)]        # rough iris centres
MOUTH = (708, 1580)                      # rough mouth centre
LASH = (60, 34, 26)
MOUTH_DARK = (120, 30, 40)
TEETH = (250, 246, 240)
TONGUE = (220, 90, 100)


def skin_ref(a, c, r0, r1):
    h, w, _ = a.shape
    ys, xs = np.mgrid[0:h, 0:w]
    d = np.hypot(xs - c[0], ys - c[1])
    ring = a[(d > r0) & (d < r1)]
    ok = ring[(ring[:, 0] > 200) & (ring[:, 1] > 130) & (ring[:, 1] < 215) & (ring[:, 2] > 90) & (ring[:, 2] < 185)]
    return np.median(ok, axis=0)


def feature(a, c, radius, skin, thresh=55):
    """Mask of non-skin pixels near c, its centre, long axis and the half-lengths along both axes."""
    h, w, _ = a.shape
    ys, xs = np.mgrid[0:h, 0:w]
    near = np.hypot(xs - c[0], ys - c[1]) < radius
    diff = np.linalg.norm(a.astype(float) - skin, axis=2) > thresh
    m = near & diff
    pts = np.stack([xs[m], ys[m]], 1).astype(float)
    mean = pts.mean(0)
    cov = np.cov((pts - mean).T)
    vals, vecs = np.linalg.eigh(cov)
    major = vecs[:, 1]
    minor = vecs[:, 0]
    pm = (pts - mean) @ major
    pn = (pts - mean) @ minor
    return m, mean, major, minor, np.percentile(np.abs(pm), 97), np.percentile(np.abs(pn), 97)


def up_for(a, centre, minor, skin):
    """Which side of the eye the eyebrow is on (dark hair-coloured pixels 26-48 px away)."""
    h, w, _ = a.shape
    ys, xs = np.mgrid[0:h, 0:w]
    d = np.hypot(xs - centre[0], ys - centre[1])
    dark = (a.sum(2) < 380) & (d > 26) & (d < 48)
    pts = np.stack([xs[dark], ys[dark]], 1).astype(float) - centre
    s = (pts @ minor).mean() if len(pts) else -1.0
    return minor if s > 0 else -minor


def fill(img, mask, colour, grow=5):
    m = Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(grow * 2 + 1))
    m = m.filter(ImageFilter.GaussianBlur(1.6))
    layer = Image.new('RGB', img.size, tuple(int(v) for v in colour))
    return Image.composite(layer, img, m)


def arc(draw, c, along, up, half, bend, width, colour, n=24):
    pts = []
    for i in range(n + 1):
        t = -1 + 2 * i / n
        p = c + along * t * half + up * bend * (1 - t * t)
        pts.append((float(p[0]), float(p[1])))
    draw.line(pts, fill=colour, width=width, joint='curve')
    for p in (pts[0], pts[-1]):
        r = width / 2
        draw.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=colour)


def ellipse_poly(c, along, up, ra, ru, n=40, start=0.0, end=2 * math.pi):
    pts = []
    for i in range(n + 1):
        t = start + (end - start) * i / n
        p = c + along * math.cos(t) * ra + up * math.sin(t) * ru
        pts.append((float(p[0]), float(p[1])))
    return pts


def main():
    src, out = sys.argv[1], sys.argv[2]
    dbg = sys.argv[3] if len(sys.argv) > 3 else None
    tex = Image.open(src).convert('RGB')
    a = np.asarray(tex)

    eyes = []
    h_, w_, _ = a.shape
    ys, xs = np.mgrid[0:h_, 0:w_]
    for c in EYES:
        skin = skin_ref(a, c, 30, 46)
        # Pass 1: the bright eye white + iris core gives the eye's axes (lashes would skew them).
        core = (np.hypot(xs - c[0], ys - c[1]) < 22) & (np.linalg.norm(a.astype(float) - skin, axis=2) > 55) & (a.sum(2) > 200)
        pts = np.stack([xs[core], ys[core]], 1).astype(float)
        mean = pts.mean(0)
        # The almond of the eye white sets the direction (the round iris would blur it).
        white = (np.hypot(xs - c[0], ys - c[1]) < 26) & (a.min(2) > 205) & (a.max(2) - a.min(2) < 40)
        wp = np.stack([xs[white], ys[white]], 1).astype(float)
        vals, vecs = np.linalg.eigh(np.cov((wp - wp.mean(0)).T))
        major, minor = vecs[:, 1], vecs[:, 0]
        up = up_for(a, mean, minor, skin)
        ha = np.percentile(np.abs((pts - mean) @ major), 97)
        hb = np.percentile(np.abs((pts - mean) @ up), 97)
        # Pass 2: everything not skin-coloured inside a generous ellipse around it (lashes included, brow excluded).
        da = (xs - mean[0]) * major[0] + (ys - mean[1]) * major[1]
        du = (xs - mean[0]) * up[0] + (ys - mean[1]) * up[1]
        inside = (da / (ha * 1.75)) ** 2 + (du / (hb * 1.9)) ** 2 < 1
        m = inside & (np.linalg.norm(a.astype(float) - skin, axis=2) > 45)
        eyes.append(dict(mask=m, c=mean, along=major, up=up, ha=ha, hb=hb, skin=skin))
    mskin = skin_ref(a, MOUTH, 30, 44)
    mm, mc, malong, mminor, mha, mhb = feature(a, MOUTH, 34, mskin, 45)
    # Face "up" at the mouth: the same way as the eye on its patch (EYES[0] shares the mouth's patch).
    mup = mminor if mminor @ eyes[0]['up'] > 0 else -mminor
    print('eyes', [(e['c'].round(1).tolist(), round(e['ha'], 1), round(e['hb'], 1)) for e in eyes])
    print('mouth', mc.round(1).tolist(), round(mha, 1), round(mhb, 1))

    def closed(img, bend_sign, width=4, bend=0.45, squeeze=1.0):
        d = ImageDraw.Draw(img)
        for e in eyes:
            img = fill(img, e['mask'], e['skin'])
            d = ImageDraw.Draw(img)
            arc(d, e['c'] - e['up'] * e['hb'] * 0.15, e['along'], e['up'], e['ha'] * 1.02 * squeeze, bend_sign * e['hb'] * bend, width, LASH)
        return img

    def mouth_shape(img, kind):
        img = fill(img, mm, mskin, 4)
        d = ImageDraw.Draw(img)
        c, al, up = mc, malong, mup
        if kind == 'smile':
            # open grin: lower half-ellipse, teeth along the top
            poly = ellipse_poly(c + up * mhb * 0.35, al, up, mha * 1.05, mhb * 1.5, start=math.pi, end=2 * math.pi)
            d.polygon(poly, fill=MOUTH_DARK)
            tongue = ellipse_poly(c - up * mhb * 0.55, al, up, mha * 0.5, mhb * 0.45)
            d.polygon(tongue, fill=TONGUE)
            teeth = ellipse_poly(c + up * mhb * 0.3, al, up, mha * 0.8, mhb * 0.38, start=math.pi, end=2 * math.pi)
            d.polygon(teeth, fill=TEETH)
            arc(d, c + up * mhb * 0.35, al, up, mha * 1.05, 0, 2, (170, 70, 75))
        elif kind == 'o':
            d.polygon(ellipse_poly(c, al, up, mha * 0.36, mhb * 0.55), fill=MOUTH_DARK)
            d.line(ellipse_poly(c, al, up, mha * 0.36, mhb * 0.55), fill=(200, 100, 105), width=2)
        elif kind == 'ouch':
            # clenched: a flat rounded bar of teeth
            poly = ellipse_poly(c, al, up, mha * 0.75, mhb * 0.5)
            d.polygon(poly, fill=TEETH)
            d.line(poly, fill=(170, 70, 75), width=3)
            d.line([tuple(c - al * mha * 0.7), tuple(c + al * mha * 0.7)], fill=(190, 170, 165), width=1)
        return img

    def surprised(img):
        # eyes a little bigger: scale each eye's pixels up around its centre
        for e in eyes:
            cx, cy = e['c']
            r = 30
            box = (int(cx - r), int(cy - r), int(cx + r), int(cy + r))
            patch = img.crop(box)
            mk = Image.fromarray((e['mask'][box[1]:box[3], box[0]:box[2]] * 255).astype(np.uint8))
            s = 1.22
            big = patch.resize((int(2 * r * s), int(2 * r * s)), Image.BICUBIC)
            bmk = mk.resize(big.size, Image.BICUBIC).filter(ImageFilter.GaussianBlur(1.2))
            off = (int(cx - r * s), int(cy - r * s))
            img.paste(big, off, bmk)
        return img

    def close_corner(img):
        # The inner corner of her right eye lives on another texture patch: paint its white and lashes over too.
        b = np.asarray(img).astype(int)
        d = np.hypot(xs - SLIVER[0], ys - SLIVER[1])
        m = (d < 30) & ((b.min(2) > 195) | (b.sum(2) < 260))
        return fill(img, m, skin_ref(b, SLIVER, 18, 40), 3)

    tiles = []
    base = tex.copy()
    tiles.append(base)
    tiles.append(close_corner(closed(tex.copy(), -1)))                                  # blink: relaxed lids
    tiles.append(close_corner(mouth_shape(closed(tex.copy(), +1, 5, 0.75), 'smile')))   # happy: ^ ^ and a grin
    tiles.append(close_corner(mouth_shape(closed(tex.copy(), 0, 5, 0.0, 0.85), 'ouch')))  # hurt: squeezed shut
    tiles.append(mouth_shape(surprised(tex.copy()), 'o'))                 # surprised

    x0, y0, x1, y1 = RECT
    w, h = x1 - x0, y1 - y0
    w2 = RECT2[2] - RECT2[0]
    # Each tile: RECT on the left, RECT2 top-right.
    atlas = Image.new('RGB', (w + w2, h * len(tiles)))
    for i, t in enumerate(tiles):
        atlas.paste(t.crop(RECT), (0, i * h))
        atlas.paste(t.crop(RECT2), (w, i * h))
    atlas.save(out)
    if dbg:
        for i, t in enumerate(tiles):
            t.save(f'{dbg}/tex_expr{i}.png')
        dbg_img = tex.crop(RECT).resize((w * 2, h * 2))
        d = ImageDraw.Draw(dbg_img)
        for e in eyes + [dict(c=mc, along=malong, up=mup, ha=mha, hb=mhb)]:
            c = (e['c'] - (x0, y0)) * 2
            d.line([tuple(c), tuple(c + e['along'] * e['ha'] * 2)], fill=(0, 255, 0), width=2)
            d.line([tuple(c), tuple(c + e['up'] * e['hb'] * 2)], fill=(255, 0, 0), width=2)
        dbg_img.save(f'{dbg}/face_axes.png')


if __name__ == '__main__':
    main()
