using System.Collections.Generic;
using Adventure3D.Gameplay;
using UnityEngine;

namespace Adventure3D.Presentation
{
    /// <summary>
    /// Combat sounds synthesised in code (no audio files needed): gunshots per weapon class, reload
    /// clicks, dry fire, hit and kill confirmation. Replace any clip later by putting a file in
    /// Resources/Adventure3D/Audio/Combat/&lt;name&gt; (for example "rifle", "pistol", "reload").
    /// </summary>
    public static class ProceduralAudio
    {
        const int Rate = 44100;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static AudioClip Shot(string weaponId, WeaponClass fallbackClass)
        {
            string file = weaponId switch
            {
                "ak47" => "ak47-1",
                "m4a1" => "m4a1_unsil-1",
                "awp" => "awp1",
                "scout" => "scout_fire-1",
                "deagle" => "deagle-1",
                "usp45" => "usp_unsil-1",
                "glock18" => "glock18-1",
                "p228" => "p228-1",
                "fiveseven" => "fiveseven-1",
                "elites" => "elite_fire",
                "mp5" => "mp5-1",
                "p90" => "p90-1",
                "tmp" => "tmp-1",
                "mac10" => "mac10-1",
                "ump45" => "ump45-1",
                "m3" => "m3-1",
                "xm1014" => "xm1014-1",
                "m249" => "m249-1",
                "galil" => "galil-1",
                "famas" => "famas-1",
                "aug" => "aug-1",
                "sg552" => "sg552-1",
                "g3sg1" => "g3sg1-1",
                "sg550" => "sg550-1",
                "knife" => "knife_slash1",
                _ => null,
            };
            if (file != null)
            {
                var clip = LoadWeaponWav(file);
                if (clip != null) return clip;
            }
            return Shot(fallbackClass);
        }

        public static AudioClip ReloadFor(string weaponId)
        {
            string file = weaponId switch
            {
                "ak47" => "ak47_clipin",
                "m4a1" => "m4a1_clipin",
                "awp" => "awp_clipin",
                "scout" => "scout_clipin",
                "deagle" => "de_clipin",
                "usp45" => "usp_clipin",
                "mp5" => "mp5_clipin",
                "p90" => "p90_clipin",
                "m3" => "m3_insertshell",
                "m249" => "m249_boxin",
                "galil" => "galil_clipin",
                "famas" => "famas_clipin",
                "aug" => "aug_clipin",
                "sg552" => "sg552_clipin",
                "g3sg1" => "g3sg1_clipin",
                "sg550" => "sg550_clipin",
                _ => "generic_reload",
            };
            return LoadWeaponWav(file) ?? Reload;
        }

        public static AudioClip EquipFor(string weaponId)
        {
            string file = weaponId switch
            {
                "knife" => "knife_deploy1",
                "awp" => "awp_deploy",
                "deagle" => "de_deploy",
                "m4a1" => "m4a1_deploy",
                "elites" => "elite_deploy",
                _ => "boltpull1",
            };
            return LoadWeaponWav(file) ?? Equip;
        }

        public static AudioClip Zoom => LoadWeaponWav("zoom") ?? Equip;

        public static AudioClip Footstep()
        {
            int idx = Random.Range(1, 5);
            return LoadFolderWav("Adventure3D/Audio/Player/", "pl_dirt" + idx);
        }

        public static AudioClip FallPain() => LoadFolderWav("Adventure3D/Audio/Player/", Random.value < 0.5f ? "pl_fallpain1" : "pl_fallpain2") ?? Hurt;
        public static AudioClip Death() => LoadFolderWav("Adventure3D/Audio/Player/", "die" + Random.Range(1, 4)) ?? Hurt;
        public static AudioClip FleshHit() => LoadFolderWav("Adventure3D/Audio/Player/", "bhit_flesh-" + Random.Range(1, 4)) ?? Hit;
        public static AudioClip ArmorHit(bool helmet) => LoadFolderWav("Adventure3D/Audio/Player/", helmet ? "bhit_helmet-1" : "bhit_kevlar-1") ?? Hit;
        public static AudioClip RoundResult(bool defendersWon) => LoadFolderWav("Adventure3D/Audio/Radio/", defendersWon ? "ctwin" : "terwin") ?? Kill;
        public static AudioClip RoundGo => LoadFolderWav("Adventure3D/Audio/Radio/", "com_go") ?? Equip;
        public static AudioClip BombPlanted => LoadFolderWav("Adventure3D/Audio/Radio/", "bombpl") ?? Streak;
        public static AudioClip BombDefused => LoadFolderWav("Adventure3D/Audio/Radio/", "bombdef") ?? Kill;
        public static AudioClip RadioClip(string name) => LoadFolderWav("Adventure3D/Audio/Radio/", name);

        static AudioClip LoadWeaponWav(string name) => LoadFolderWav("Adventure3D/Audio/Weapons/", name);

        static AudioClip LoadFolderWav(string folder, string name)
        {
            string key = folder + name;
            if (Cache.TryGetValue(key, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>(key);
            if (c != null) Cache[key] = c;
            return c;
        }

        public static AudioClip Shot(WeaponClass c) => c switch
        {
            WeaponClass.Pistol => Get("pistol", () => Gun(0.32f, 0.55f, 95f, 0.35f, 3)),
            WeaponClass.SMG => Get("smg", () => Gun(0.22f, 0.5f, 110f, 0.3f, 5)),
            WeaponClass.Rifle => Get("rifle", () => Gun(0.42f, 0.75f, 70f, 0.5f, 7)),
            WeaponClass.Sniper => Get("sniper", () => Gun(0.9f, 1f, 50f, 0.7f, 11)),
            WeaponClass.Shotgun => Get("shotgun", () => Gun(0.65f, 0.95f, 55f, 0.65f, 13)),
            WeaponClass.Heavy => Get("heavy", () => Gun(0.38f, 0.8f, 65f, 0.5f, 17)),
            _ => Get("knife", Swish),
        };

        public static AudioClip Reload => Get("reload", () => Clicks(new[] { 0.05f, 0.45f, 0.55f, 1.25f }, 1.4f, 19));
        public static AudioClip DryFire => LoadWeaponWav("dryfire_rifle") ?? Get("dry", () => Clicks(new[] { 0f }, 0.12f, 23));
        public static AudioClip Hit => Get("hit", () => Tone(1450f, 0.06f, 0.35f));
        public static AudioClip Headshot => LoadWeaponWav("headshot2") ?? Get("headshot", () => Tone(2300f, 0.12f, 0.4f));
        public static AudioClip Kill => Get("kill", () => TwoTone(880f, 1320f, 0.22f, 0.35f));
        public static AudioClip Hurt => Get("hurt", () => Gun(0.18f, 0.35f, 140f, 0.15f, 29));
        public static AudioClip Heartbeat => Get("heartbeat", () => Thumps(new[] { 0f, 0.22f }, 0.6f));
        public static AudioClip Streak => Get("streak", () => TwoTone(660f, 990f, 0.35f, 0.4f));
        public static AudioClip Equip => Get("equip", () => Clicks(new[] { 0f, 0.12f }, 0.3f, 31));

        static AudioClip Get(string name, System.Func<AudioClip> make)
        {
            if (Cache.TryGetValue(name, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>("Adventure3D/Audio/Combat/" + name);
            if (c == null) c = make();
            Cache[name] = c;
            return c;
        }

        /// <summary>Noise burst with a sharp attack, a low "thump" and an exponential tail.</summary>
        static AudioClip Gun(float length, float gain, float thumpHz, float body, int seed)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float crack = noise * Mathf.Exp(-t * 55f);                       // bright transient
                lp += (noise - lp) * 0.08f;                                       // low-passed rumble
                lp2 += (lp - lp2) * 0.15f;
                float tail = lp2 * 3.2f * Mathf.Exp(-t * (6f / length));
                float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t * (1f - t * 0.8f)) * Mathf.Exp(-t * 18f) * body;
                data[i] = Mathf.Clamp((crack * 0.9f + tail + thump) * gain, -1f, 1f);
            }
            return Make("gun_" + seed, data);
        }

        static AudioClip Swish()
        {
            int n = (int)(Rate * 0.22f);
            var data = new float[n];
            var rng = new System.Random(5);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (noise - lp) * Mathf.Lerp(0.05f, 0.4f, t);
                data[i] = lp * Mathf.Sin(Mathf.PI * t) * 0.6f;
            }
            return Make("swish", data);
        }

        static AudioClip Clicks(float[] times, float length, int seed)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            var rng = new System.Random(seed);
            foreach (float start in times)
            {
                int s = (int)(start * Rate);
                for (int i = 0; i < Rate * 0.05f && s + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                    data[s + i] += (noise * 0.5f + Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.4f) * Mathf.Exp(-t * 160f);
                }
            }
            return Make("clicks_" + seed, data);
        }

        static AudioClip Thumps(float[] times, float length)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            foreach (float start in times)
            {
                int s = (int)(start * Rate);
                for (int i = 0; i < Rate * 0.18f && s + i < n; i++)
                {
                    float t = i / (float)Rate;
                    data[s + i] += Mathf.Sin(2f * Mathf.PI * 52f * t) * Mathf.Exp(-t * 22f) * 0.8f;
                }
            }
            return Make("thumps", data);
        }

        static AudioClip Tone(float hz, float length, float gain)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-t * 40f) * gain;
            }
            return Make("tone_" + hz, data);
        }

        static AudioClip TwoTone(float a, float b, float length, float gain)
        {
            int n = (int)(Rate * length);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float hz = t < length * 0.45f ? a : b;
                data[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * Mathf.Exp(-(t % (length * 0.45f)) * 18f) * gain;
            }
            return Make("twotone", data);
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
