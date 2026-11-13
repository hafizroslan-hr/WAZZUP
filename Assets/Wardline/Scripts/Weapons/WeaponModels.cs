using UnityEngine;

namespace Wardline
{
    /// <summary>Builds original low-poly weapon models from primitives, driven by WeaponDef shape parameters.</summary>
    public static class WeaponModels
    {
        public class Parts
        {
            public GameObject root;
            public Transform magazine, muzzle, ejection, slide, leftHand;
        }

        public static GameObject BuildWorld(WeaponDef def, Transform parent) => Build(def, parent, 1f).root;

        public static Parts Build(WeaponDef def, Transform parent, float scale)
        {
            var p = new Parts();
            p.root = new GameObject(def.name);
            p.root.transform.SetParent(parent, false);
            var t = p.root.transform;
            var body = MaterialLib.Lit(def.bodyColor, null, 0.35f, 0.4f);
            var accent = MaterialLib.Lit(def.accentColor, null, 0.25f);
            var dark = MaterialLib.Lit(new Color(0.05f, 0.05f, 0.06f), null, 0.5f, 0.5f);
            float L = def.modelLength * scale, B = def.barrelLength * scale;

            switch (def.category)
            {
                case WeaponCategory.Knife:
                {
                    MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, 0, -0.03f) * scale, new Vector3(0.03f, 0.035f, 0.12f) * scale, dark);
                    MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, 0.005f, 0.11f) * scale, new Vector3(0.008f, 0.032f, 0.18f) * scale, MaterialLib.Lit(new Color(0.75f, 0.77f, 0.8f), null, 0.85f, 0.9f));
                    MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, 0, 0.03f) * scale, new Vector3(0.05f, 0.05f, 0.012f) * scale, dark);
                    break;
                }
                case WeaponCategory.Grenade:
                {
                    Color c = def.grenade == GrenadeType.HE ? new Color(0.25f, 0.32f, 0.2f) : def.grenade == GrenadeType.Flash ? new Color(0.6f, 0.62f, 0.65f) : new Color(0.35f, 0.38f, 0.42f);
                    if (def.grenade == GrenadeType.HE) MeshUtil.Visual(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.07f * scale, MaterialLib.Lit(c, null, 0.3f));
                    else MeshUtil.Visual(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(0.055f, 0.05f, 0.055f) * scale, MaterialLib.Lit(c, null, 0.4f, 0.5f));
                    MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, 0.05f, 0) * scale, new Vector3(0.02f, 0.03f, 0.04f) * scale, dark);
                    break;
                }
                case WeaponCategory.Bomb:
                {
                    MeshUtil.Visual(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.22f, 0.09f, 0.16f) * scale, MaterialLib.Lit(new Color(0.3f, 0.26f, 0.18f)));
                    MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, 0.05f, 0.02f) * scale, new Vector3(0.12f, 0.02f, 0.07f) * scale, dark);
                    MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0.03f, 0.065f, 0.02f) * scale, new Vector3(0.04f, 0.01f, 0.03f) * scale, MaterialLib.Unlit(new Color(1f, 0.2f, 0.1f)));
                    break;
                }
                case WeaponCategory.Pistol:
                {
                    bool dual = def.id == "twin9";
                    for (int i = 0; i < (dual ? 2 : 1); i++)
                    {
                        var g = new GameObject("Pistol").transform; g.SetParent(t, false);
                        g.localPosition = dual ? new Vector3((i == 0 ? 0.14f : -0.14f) * scale, 0, 0) : Vector3.zero;
                        var slide = MeshUtil.Visual(PrimitiveType.Cube, g, new Vector3(0, 0.02f * scale, L * 0.1f), new Vector3(0.032f * scale, 0.035f * scale, L), body);
                        MeshUtil.Visual(PrimitiveType.Cube, g, new Vector3(0, -0.045f * scale, -L * 0.25f), new Vector3(0.028f * scale, 0.09f * scale, 0.045f * scale), dark, Quaternion.Euler(-12, 0, 0));
                        if (B > 0.06f * scale) MeshUtil.Visual(PrimitiveType.Cylinder, g, new Vector3(0, 0.02f * scale, L * 0.6f + B * 0.5f), new Vector3(0.03f * scale, B * 0.5f, 0.03f * scale), dark, Quaternion.Euler(90, 0, 0));
                        if (i == 0) { p.slide = slide.transform; p.magazine = g; }
                    }
                    p.muzzle = Point(t, new Vector3(0, 0.02f * scale, L * 0.6f + B + 0.02f * scale));
                    p.ejection = Point(t, new Vector3(0.02f * scale, 0.04f * scale, L * 0.1f));
                    break;
                }
                default:
                {
                    // Receiver
                    var receiver = MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, 0, 0), new Vector3(0.05f * scale, 0.07f * scale, L * 0.55f), body);
                    p.slide = receiver.transform;
                    // Barrel
                    float barrelZ = L * 0.275f + B * 0.5f;
                    float barrelR = def.category == WeaponCategory.Shotgun ? 0.034f : def.category == WeaponCategory.Sniper ? 0.026f : 0.022f;
                    MeshUtil.Visual(PrimitiveType.Cylinder, t, new Vector3(0, 0.015f * scale, barrelZ), new Vector3(barrelR * scale, B * 0.5f, barrelR * scale), dark, Quaternion.Euler(90, 0, 0));
                    // Handguard
                    MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, -0.005f * scale, L * 0.3f), new Vector3(0.055f * scale, 0.06f * scale, L * 0.25f), accent);
                    // Grip
                    MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, -0.07f * scale, -L * 0.12f), new Vector3(0.035f * scale, 0.1f * scale, 0.045f * scale), dark, Quaternion.Euler(-15, 0, 0));
                    // Stock
                    if (def.hasStock) MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, -0.015f * scale, -L * 0.42f), new Vector3(0.04f * scale, 0.085f * scale, L * 0.3f), accent);
                    // Magazine
                    var magRoot = new GameObject("Mag").transform; magRoot.SetParent(t, false);
                    magRoot.localPosition = new Vector3(0, -0.05f * scale, L * 0.08f);
                    if (def.drumMag) MeshUtil.Visual(PrimitiveType.Cube, magRoot, new Vector3(0, -0.06f * scale, 0), new Vector3(0.1f * scale, 0.11f * scale, 0.13f * scale), dark);
                    else if (def.boxMag) MeshUtil.Visual(PrimitiveType.Cube, magRoot, new Vector3(0, -0.07f * scale, -0.06f * scale), new Vector3(0.03f * scale, 0.14f * scale, 0.035f * scale), dark);
                    else if (def.category == WeaponCategory.Shotgun) MeshUtil.Visual(PrimitiveType.Cylinder, magRoot, new Vector3(0, 0.03f * scale, L * 0.22f), new Vector3(0.03f * scale, L * 0.2f, 0.03f * scale), dark, Quaternion.Euler(90, 0, 0));
                    else
                    {
                        float curve = def.id == "ar47" || def.id == "gale" ? 20f : 8f;
                        MeshUtil.Visual(PrimitiveType.Cube, magRoot, new Vector3(0, -0.07f * scale, 0.015f * scale), new Vector3(0.03f * scale, 0.15f * scale, 0.05f * scale), dark, Quaternion.Euler(curve, 0, 0));
                    }
                    p.magazine = magRoot;
                    // Optic / carry rail
                    if (def.hasScope)
                    {
                        float sl = def.category == WeaponCategory.Sniper ? 0.3f : 0.16f;
                        float sr = def.category == WeaponCategory.Sniper ? 0.034f : 0.03f;
                        MeshUtil.Visual(PrimitiveType.Cylinder, t, new Vector3(0, 0.075f * scale, 0.02f * scale), new Vector3(sr * scale, sl * 0.5f * scale, sr * scale), dark, Quaternion.Euler(90, 0, 0));
                        MeshUtil.Visual(PrimitiveType.Cylinder, t, new Vector3(0, 0.075f * scale, (0.02f + sl * 0.5f) * scale), new Vector3(sr * 1.25f * scale, 0.02f * scale, sr * 1.25f * scale), MaterialLib.Lit(new Color(0.15f, 0.3f, 0.5f), null, 0.95f), Quaternion.Euler(90, 0, 0));
                    }
                    else MeshUtil.Visual(PrimitiveType.Cube, t, new Vector3(0, 0.045f * scale, -L * 0.05f), new Vector3(0.012f * scale, 0.02f * scale, L * 0.3f), dark);
                    p.muzzle = Point(t, new Vector3(0, 0.015f * scale, L * 0.275f + B + 0.02f * scale));
                    p.ejection = Point(t, new Vector3(0.03f * scale, 0.02f * scale, L * 0.05f));
                    p.leftHand = Point(t, new Vector3(0, -0.03f * scale, L * 0.3f));
                    break;
                }
            }
            if (p.muzzle == null) p.muzzle = Point(t, new Vector3(0, 0, L * 0.5f));
            return p;
        }

        static Transform Point(Transform parent, Vector3 pos)
        {
            var g = new GameObject("Point").transform; g.SetParent(parent, false); g.localPosition = pos; return g;
        }
    }
}
