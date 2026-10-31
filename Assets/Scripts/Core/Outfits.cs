using System.Collections.Generic;
using UnityEngine;

namespace MayaAdventure
{
    /// <summary>
    /// Maya's wardrobe: clothes and accessories bought with coins. Every coin she ever picks up also goes into a
    /// wallet that survives between games; purchases and what she wears are saved on the device.
    /// </summary>
    public static class Outfits
    {
        public struct Item
        {
            public string Id, Name;
            public int Price;
            /// <summary>Items in the same group replace each other (one jacket colour at a time).</summary>
            public string Group;
            public Color Tint;
        }

        public static readonly Item[] Items =
        {
            new Item { Id = "crown", Name = "כתר זהב", Price = 60 },
            new Item { Id = "cape", Name = "גלימת גיבורה", Price = 100 },
            new Item { Id = "jacket_purple", Name = "מעיל סגול", Price = 40, Group = "jacket", Tint = new Color(0.62f, 0.42f, 0.95f) },
            new Item { Id = "jacket_blue", Name = "מעיל תכלת", Price = 40, Group = "jacket", Tint = new Color(0.4f, 0.72f, 1f) },
            new Item { Id = "jacket_green", Name = "מעיל ירוק", Price = 40, Group = "jacket", Tint = new Color(0.45f, 0.85f, 0.5f) },
        };

        const string WalletKey = "maya_wallet", OwnedKey = "maya_owned", WornKey = "maya_worn";
        static readonly HashSet<string> owned = new HashSet<string>(), worn = new HashSet<string>();
        static bool loaded;

        /// <summary>Raised when Maya's look changes (bought, put on, taken off).</summary>
        public static event System.Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            owned.Clear();
            worn.Clear();
            foreach (var id in PlayerPrefs.GetString(OwnedKey, "").Split(',')) if (id.Length > 0) owned.Add(id);
            foreach (var id in PlayerPrefs.GetString(WornKey, "").Split(',')) if (id.Length > 0) worn.Add(id);
        }

        static void Save()
        {
            PlayerPrefs.SetString(OwnedKey, string.Join(",", owned));
            PlayerPrefs.SetString(WornKey, string.Join(",", worn));
            PlayerPrefs.Save();
        }

        public static int Wallet => PlayerPrefs.GetInt(WalletKey, 0);

        public static void AddCoins(int n)
        {
            if (n <= 0) return;
            PlayerPrefs.SetInt(WalletKey, Wallet + n); // saved with the next PlayerPrefs.Save (stars, settings, quit)
        }

        public static bool Owns(string id)
        {
            Load();
            return owned.Contains(id);
        }

        public static bool Wears(string id)
        {
            Load();
            return worn.Contains(id);
        }

        static Item Find(string id)
        {
            foreach (var it in Items) if (it.Id == id) return it;
            return default;
        }

        /// <summary>Buy (if affordable) and put on. Returns false when there aren't enough coins.</summary>
        public static bool Buy(string id)
        {
            Load();
            var item = Find(id);
            if (item.Id == null || owned.Contains(id)) return owned.Contains(id);
            if (Wallet < item.Price) return false;
            PlayerPrefs.SetInt(WalletKey, Wallet - item.Price);
            owned.Add(id);
            Wear(id, true);
            return true;
        }

        public static void Wear(string id, bool on)
        {
            Load();
            var item = Find(id);
            if (on && item.Group != null)
                foreach (var other in Items)
                    if (other.Group == item.Group) worn.Remove(other.Id);
            if (on) worn.Add(id); else worn.Remove(id);
            Save();
            Changed?.Invoke();
        }

        /// <summary>The jacket colour Maya wears, or clear (alpha 0) for her original pink.</summary>
        public static Color JacketTint
        {
            get
            {
                Load();
                foreach (var it in Items)
                    if (it.Group == "jacket" && worn.Contains(it.Id)) return new Color(it.Tint.r, it.Tint.g, it.Tint.b, 1f);
                return Color.clear;
            }
        }
    }
}
