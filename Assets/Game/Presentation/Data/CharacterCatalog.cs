using System;
using System.Collections.Generic;
using Adventure3D.Core;
using UnityEngine;

namespace Adventure3D.Presentation
{
    public enum CharacterQuality
    {
        Hd = 0,
        Classic = 1,
    }

    public sealed class CharacterSpec
    {
        public string Id;
        public Team Team;
        public string ArabicName;
        public string CodeName;
        public string RoleTag;
        public string Description;
        public bool HasClassic = true;
        public bool HasHd = true;
        public bool ExcludeFromBotPool;
    }

    /// <summary>
    /// Catalog of all imported Counter-Strike 1.6 characters (HD and Classic GLB models)
    /// with team assignment, Arabic localization, and cached prefab loading.
    /// </summary>
    public static class CharacterCatalog
    {
        public const string FallbackResource = "Adventure3D/Characters/Operative";

        public static readonly CharacterSpec[] All =
        {
            // Defenders (CT)
            new CharacterSpec
            {
                Id = "sas",
                Team = Team.Defenders,
                ArabicName = "قوات SAS الخاصة",
                CodeName = "SAS",
                RoleTag = "اقتحام ليلي",
                Description = "وحدة النخبة البريطانية المتخصصة في مكافحة الإرهاب والاقتحام الدقيق.",
            },
            new CharacterSpec
            {
                Id = "gsg9",
                Team = Team.Defenders,
                ArabicName = "وحدة GSG-9",
                CodeName = "GSG-9",
                RoleTag = "تفكيك القنابل",
                Description = "فرقة التدخل الألمانية المعروفة بسرعة الاستجابة وتأمين الرهائن.",
            },
            new CharacterSpec
            {
                Id = "gign",
                Team = Team.Defenders,
                ArabicName = "فرقة GIGN",
                CodeName = "GIGN",
                RoleTag = "تدخل سريع",
                Description = "وحدة العمليات الخاصة الفرنسية ذات التجهيز التكتيكي الثقيل.",
            },
            new CharacterSpec
            {
                Id = "urban",
                Team = Team.Defenders,
                ArabicName = "العمليات البحرية SEAL",
                CodeName = "URBAN",
                RoleTag = "قتال مدني",
                Description = "فريق العمليات التكتيكية للقتال داخل المدن والمناطق المحصنة.",
            },
            new CharacterSpec
            {
                Id = "spetsnaz",
                Team = Team.Defenders,
                ArabicName = "قوات Spetsnaz",
                CodeName = "SPETSNAZ",
                RoleTag = "مهام خاصة",
                Description = "وحدة الاقتحام الخاصة عالية التحمل في الاشتباكات القريبة.",
                HasClassic = false,
            },
            new CharacterSpec
            {
                Id = "vip",
                Team = Team.Defenders,
                ArabicName = "الشخصية الهامة VIP",
                CodeName = "VIP",
                RoleTag = "حماية خاصة",
                Description = "هدف الحماية الرئيسي في مهمات الإخلاء والتأمين التكتيكي.",
                ExcludeFromBotPool = true,
            },

            // Attackers (T)
            new CharacterSpec
            {
                Id = "terror",
                Team = Team.Attackers,
                ArabicName = "فصيل فينيكس",
                CodeName = "PHOENIX",
                RoleTag = "هجوم مباشر",
                Description = "تشكيل قتالي منظم يتميز بالانتشار السريع والضغط الهجومي.",
            },
            new CharacterSpec
            {
                Id = "leet",
                Team = Team.Attackers,
                ArabicName = "فرقة النخبة",
                CodeName = "ELITE CREW",
                RoleTag = "قتال صحراوي",
                Description = "مقاتلون متمرسون في البيئات الصحراوية والمواجهات بعيدة المدى.",
            },
            new CharacterSpec
            {
                Id = "arctic",
                Team = Team.Attackers,
                ArabicName = "مقاتل القطب",
                CodeName = "ARCTIC",
                RoleTag = "تمويه ثلجي",
                Description = "وحدة استطلاع وهجوم مجهزة للعمل في الظروف القاسية.",
            },
            new CharacterSpec
            {
                Id = "guerilla",
                Team = Team.Attackers,
                ArabicName = "حرب العصابات",
                CodeName = "GUERILLA",
                RoleTag = "كمين ومناورة",
                Description = "مقاتلون خفيفو الحركة يعتمدون على الالتفاف والمباغتة.",
            },
            new CharacterSpec
            {
                Id = "militia",
                Team = Team.Attackers,
                ArabicName = "الميليشيا الميدانية",
                CodeName = "MILITIA",
                RoleTag = "دعم هجومي",
                Description = "تشكيل ميداني مسلح متخصص في السيطرة على المواقع المفتوحة.",
                HasClassic = false,
            },
        };

        static readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        public static List<CharacterSpec> ForTeam(Team team)
        {
            var list = new List<CharacterSpec>();
            for (int i = 0; i < All.Length; i++)
                if (All[i].Team == team) list.Add(All[i]);
            return list;
        }

        public static CharacterSpec GetById(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                for (int i = 0; i < All.Length; i++)
                    if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase))
                        return All[i];
            }
            return All[0];
        }

        public static CharacterSpec GetForBot(Team team, ushort playerId)
        {
            var pool = new List<CharacterSpec>();
            for (int i = 0; i < All.Length; i++)
                if (All[i].Team == team && !All[i].ExcludeFromBotPool)
                    pool.Add(All[i]);
            if (pool.Count == 0) return All[0];
            return pool[playerId % pool.Count];
        }

        public static string ResourcePath(string id, CharacterQuality quality)
        {
            var spec = GetById(id);
            bool useClassic = quality == CharacterQuality.Classic && spec.HasClassic;
            string folder = useClassic ? "Classic" : "HD";
            return $"Adventure3D/Characters/{folder}/{spec.Id}";
        }

        public static GameObject LoadModelPrefab(string id, CharacterQuality quality)
        {
            string path = ResourcePath(id, quality);
            if (_cache.TryGetValue(path, out var cached) && cached != null)
                return cached;

            var loaded = Resources.Load<GameObject>(path);
            if (loaded == null && quality == CharacterQuality.Classic)
            {
                string hdPath = ResourcePath(id, CharacterQuality.Hd);
                loaded = Resources.Load<GameObject>(hdPath);
            }
            if (loaded == null)
                loaded = Resources.Load<GameObject>(FallbackResource);

            _cache[path] = loaded;
            return loaded;
        }
    }
}
