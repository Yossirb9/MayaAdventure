using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// A friendly little character near each world's start. When Maya comes close it turns to her and talks:
    /// short Hebrew tips about the world, one after another.
    /// </summary>
    public class Npc : MonoBehaviour
    {
        public string Who;
        public string[] Lines;
        int line;
        float lineTime, bob;
        Transform body, marker;
        bool near;

        public static Npc Build(Transform parent, Vector3 position, float yaw, string model, string who, params string[] lines)
        {
            var go = new GameObject("Npc " + who);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var npc = go.AddComponent<Npc>();
            npc.Who = who;
            npc.Lines = lines;
            npc.body = new GameObject("Body").transform;
            npc.body.SetParent(go.transform, false);
            Props.Spawn(model, npc.body, Vector3.zero, 1.7f, 180f);
            // A soft speech-bubble gem bobbing above the head: "I have something to say".
            var gem = Nature.Load("Platformer/Gem_Blue");
            if (gem != null)
            {
                npc.marker = Instantiate(gem, go.transform, false).transform;
                npc.marker.localPosition = new Vector3(0f, 2.1f, 0f);
                npc.marker.localScale = Vector3.one * 0.35f;
                foreach (var c in npc.marker.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            var cap = go.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.7f, 0f);
            cap.height = 1.4f;
            cap.radius = 0.45f;
            return npc;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.Player == null) return;
            float dt = Time.deltaTime;
            bob += dt;
            body.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(bob * (near ? 5f : 2f))) * (near ? 0.12f : 0.05f);
            if (marker != null)
            {
                marker.Rotate(0f, 120f * dt, 0f, Space.World);
                marker.localPosition = new Vector3(0f, 2.1f + Mathf.Sin(bob * 2.4f) * 0.1f, 0f);
                marker.gameObject.SetActive(line < Lines.Length);
            }
            var to = gm.Player.transform.position - transform.position;
            to.y = 0f;
            bool wasNear = near;
            near = gm.State == GameState.Playing && to.sqrMagnitude < 3.4f * 3.4f;
            if (!near) return;
            // Turn to Maya and talk; each tip stays up for a few seconds, then the next one.
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 360f * dt);
            if (!wasNear)
            {
                if (line >= Lines.Length) line = 0;
                lineTime = 0f;
                gm.Sfx.Play(gm.Sfx.Coin, 0.35f);
            }
            lineTime += dt;
            if (lineTime > 3.8f && line < Lines.Length - 1)
            {
                line++;
                lineTime = 0f;
                gm.Sfx.Play(gm.Sfx.Coin, 0.25f);
            }
            else if (lineTime > 3.8f && line == Lines.Length - 1) line = Lines.Length; // all said: the marker goes away
            if (line < Lines.Length) gm.Hud.Speech(Who, Lines[line]);
        }

        /// <summary>The guide of each world, standing a few steps from where Maya arrives.</summary>
        public static void ForLevel(int level, LevelInfo info)
        {
            var side = Quaternion.Euler(0f, info.StartYaw, 0f);
            var p = LevelBuilder.Snap(info.Start + side * new Vector3(3.2f, 0f, 3f), info.Start.y + 3f);
            float yaw = info.StartYaw + 200f;
            switch (level)
            {
                case 0:
                    Build(info.Root, p, yaw, "character-oodi", "בובי",
                          "היי מאיה! ברוכה הבאה לאי השמש!",
                          "כל כוכב פותח שערים לעולמות חדשים. העמוד הזוהר מראה לך את המשימה הבאה.",
                          "שמונה מטבעות אדומים מסתתרים באי. מה יקרה אם תאספי את כולם?",
                          "ומהגרמבלים כדאי להיזהר: קפצי להם על הראש או תני להם מכה!");
                    break;
                case 1:
                    Build(info.Root, p, yaw, "character-ooli", "פיפי",
                          "ברררר! קר פה, נכון?",
                          "התותח יעיף אותך רחוק. היכנסי אליו ואל תפחדי!",
                          "איש שלג ענק שומר על אחד הכוכבים. קפצי לו על הראש שלוש פעמים!");
                    break;
                case 2:
                    Build(info.Root, p, yaw, "character-oopi", "קורי",
                          "בלוב בלוב! הלגונה מלאה באוצרות.",
                          "כדי לצלול, החזיקי את כפתור המכה. כדי לעלות, לחצי קפיצה.",
                          "תמנון ענק מסתובב על האי הקטן. היזהרי מכדורי הדיו שלו!");
                    break;
                case 3:
                    Build(info.Root, p, yaw, "character-oozi", "זוזי",
                          "רק לא ליפול מהמבצר!",
                          "בתיבה האדומה יש כובע כנפיים. החזיקי קפיצה כדי לרחף.",
                          "המסורים זזים מהר. חכי לרגע הנכון ורוצי!");
                    break;
                case 4:
                    Build(info.Root, p, yaw, "character-oobi", "לולו",
                          "ששש... המלך גרמבל ממש קרוב.",
                          "כשהוא קופץ, יוצאת ממנו טבעת אש. קפצי מעליה!",
                          "וכשהוא כועס הוא מהיר יותר. שלוש קפיצות על הראש והכוכב שלך!");
                    break;
            }
        }
    }
}
