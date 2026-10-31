using UnityEngine;

namespace MayaAdventure
{
    public enum CrateContents { Coins, BlueCoin, Heart, OneUp, Wing, Metal }

    /// <summary>Wooden crate: punch it (or ground-pound next to it) to break it open.</summary>
    public class Crate : Punchable
    {
        CrateContents contents;
        int amount;
        bool broken;

        public static Crate Build(Transform parent, Vector3 position, CrateContents contents = CrateContents.Coins, int amount = 3, float yaw = 0f)
        {
            var go = new GameObject("Crate");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (Props.Has("crate")) Props.Spawn("crate", go.transform, Vector3.zero, 2.4f);
            else Art.Prim(PrimitiveType.Cube, "Box", go.transform, new Vector3(0f, 0.6f, 0f), Vector3.one * 1.2f, Art.Textured("plank", Vector2.one));
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.6f, 0f);
            box.size = Vector3.one * 1.2f;
            var c = go.AddComponent<Crate>();
            c.contents = contents;
            c.amount = amount;
            return c;
        }

        public override Vector3 PunchPoint => transform.position + Vector3.up * 0.6f;
        public override float Radius => 0.75f;

        public override void OnPunched(Vector3 from)
        {
            if (broken) return;
            broken = true;
            var gm = GameManager.I;
            Vector3 c = PunchPoint;
            Debris.Burst(c, new Color(0.8f, 0.55f, 0.3f), 14, 5f);
            if (gm != null) gm.Sfx.Play(gm.Sfx.Stomp, 0.8f);
            var parent = transform.parent;
            var p = transform.localPosition;
            switch (contents)
            {
                case CrateContents.Coins:
                    for (int i = 0; i < amount; i++) Coin.Build(parent, p + Vector3.up * 0.9f + LevelBuilder.Polar(i * 360f / amount, 1.1f));
                    break;
                case CrateContents.BlueCoin:
                    Coin.BuildBlue(parent, p + Vector3.up * 1f);
                    break;
                case CrateContents.Heart:
                    Heart.Build(parent, p + Vector3.up * 1f);
                    break;
                case CrateContents.OneUp:
                    OneUp.Build(parent, p + Vector3.up * 0.05f);
                    break;
                case CrateContents.Wing:
                    PowerCap.Build(parent, p + Vector3.up * 1.1f, PowerKind.Wing);
                    break;
                case CrateContents.Metal:
                    PowerCap.Build(parent, p + Vector3.up * 1.1f, PowerKind.Metal);
                    break;
            }
            Destroy(gameObject);
        }
    }
}
