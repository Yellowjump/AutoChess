using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using DataTable;

namespace Entity
{
    public sealed partial class TimelineBurnVisuals
    {
        private Material cloudMaterial;

        private Material tideBellMetal;
        private GameObject tideBellModel;

        internal Transform RaiseTideBell(Vector3 ground)
        {
            if (tideBellModel == null) tideBellModel = Resources.Load<GameObject>("TideChimeBell");
            if (tideBellMetal == null) tideBellMetal = new Material(Resources.Load<Shader>("TideBellBronze"));
            var pivot = new GameObject("叠潮铃 · 悬铃").transform;
            pivot.SetParent(transform, false);
            var model = Instantiate(tideBellModel, pivot, false);
            var renderer = model.GetComponentInChildren<MeshRenderer>();
            renderer.sharedMaterial = tideBellMetal;
            var bounds = renderer.bounds;
            float scale = 1.25f / bounds.size.y;
            model.transform.localPosition = -new Vector3(bounds.center.x - pivot.position.x,
                bounds.max.y - pivot.position.y, bounds.center.z - pivot.position.z) * scale;
            model.transform.localScale *= scale;
            PoseTideBell(pivot, ground, 0, 0);
            return pivot;
        }

        internal static void PoseTideBell(Transform pivot, Vector3 ground, float age, int strikes)
        {
            var camera = Camera.main;
            Vector3 right = camera == null ? Vector3.right : Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            pivot.position = ground + right * .93f + Vector3.up * (2.65f + .045f * Mathf.Sin(age * 4f));
            float elapsed = strikes == 2 ? age - 1f : age - .1f;
            float swing = strikes == 0 ? 0f : 31f * Mathf.Exp(-4.9f * elapsed) * Mathf.Cos(14f * elapsed);
            if (strikes == 2) swing = -swing;
            pivot.rotation = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up)) * Quaternion.Euler(-8f, 0, swing);
            pivot.localScale = Vector3.one * Mathf.Lerp(.05f, 1f, Mathf.Clamp01((1.85f - age) / .18f));
        }

        internal void TideChimePulse(Vector3 center, float radius)
        {
            Ring(center, radius, .42f, .13f, Water);
            Ring(center, radius * .89f, .51f, .055f, ThunderSilver);
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.TideBell, center);
        }

        internal Transform SummonFocus(bool cloud, Vector3 ground, Vector3 direction)
        {
            var root = new GameObject(cloud ? "唤雷云" : "金光镜").transform;
            root.SetParent(transform, false);
            root.position = ground + Vector3.up * (cloud ? 2f : .85f);
            if (cloud) CreateCloud(root);
            else { CreateMirror(root); PoseMirror(root, ground, direction, 0f); }
            HotGameEntry.Sound.PlayTimelineCue(cloud ? EnumSound.StormGather : EnumSound.MirrorChime, ground);
            return root;
        }

        private void CreateCloud(Transform root)
        {
            var system = root.gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.95f, 1.5f);
            main.startSpeed = .065f;
            main.startSize = new ParticleSystem.MinMaxCurve(.7f, 1.05f);
            main.maxParticles = 48;
            main.prewarm = true;
            var emission = system.emission;
            emission.rateOverTime = 25;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f;
            shape.scale = new Vector3(.82f, .16f, .44f);
            var tint = system.colorOverLifetime;
            tint.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.96f, .2f),
                    new GradientAlphaKey(.95f, .72f), new GradientAlphaKey(0, 1) });
            tint.color = gradient;
            if (cloudMaterial == null) cloudMaterial = new Material(Resources.Load<Shader>("StormVapour"));
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = cloudMaterial;
            system.Play();
        }

        private Mesh mirrorMesh;
        private Material mirrorMetal, rayMaterial;
        private MaterialPropertyBlock mirrorProperties;

        private void CreateMirror(Transform root)
        {
            if (mirrorMesh == null)
            {
                // A convex face, bevelled flower rim and raised boss share one closed mesh.
                const int segments = 128;
                float[] radii = { 0, .18f, .64f, .83f, .96f, 1f, 1f, .96f, .86f, .77f, .29f, .20f, .13f, 0 };
                float[] depth = { .13f,.126f,.096f,.07f,.09f,.061f,-.054f,-.096f,-.11f,-.082f,-.09f,-.12f,-.19f,-.21f };
                var points = new List<Vector3>();
                var triangles = new List<int>();
                for (int ring = 0; ring < radii.Length; ring++)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        float a = 2f * Mathf.PI * i / segments;
                        float r = radii[ring] * (.78f + .065f * Mathf.Cos(8f * a) * Mathf.SmoothStep(0f, 1f, radii[ring]));
                        points.Add(new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), depth[ring]));
                        if (ring == 0) continue;
                        int k = ring * segments + i, n = ring * segments + (i + 1) % segments;
                        triangles.Add(k - segments); triangles.Add(k); triangles.Add(n);
                        triangles.Add(k - segments); triangles.Add(n); triangles.Add(n - segments);
                    }
                }
                mirrorProperties = new MaterialPropertyBlock();
                mirrorMesh = new Mesh { name = "八瓣鎏金古镜" };
                mirrorMesh.SetVertices(points);
                mirrorMesh.SetTriangles(triangles, 0);
                mirrorMesh.RecalculateNormals();
                mirrorMetal = new Material(Resources.Load<Shader>("OctofoilBronze"));
                rayMaterial = new Material(Resources.Load<Shader>("MirrorSunRay"));
            }
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mirrorMesh;
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mirrorMetal;
        }

        internal void PoseMirror(Transform root, Vector3 ground, Vector3 forward, float age)
        {
            float rise = Mathf.SmoothStep(0f, 1f, age / SummonedArt.ChargeSeconds);
            float remaining = SummonedArt.ChargeSeconds + SummonedArt.BeamSeconds + SummonedArt.AfterglowSeconds - age;
            float tail = Mathf.SmoothStep(0f, 1f, remaining / SummonedArt.AfterglowSeconds);
            root.position = ground + Vector3.up * Mathf.Lerp(.93f, 1.68f, rise);
            root.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0, (1f-rise)*56f, (1f-rise)*-32f);
            root.localScale = Vector3.one * Mathf.Lerp(.3f, 1f, rise) * Mathf.Lerp(.45f, 1f, tail);
            mirrorProperties.SetFloat("_Charge", rise * tail);
            root.GetComponent<MeshRenderer>().SetPropertyBlock(mirrorProperties);
        }

        internal LineRenderer MirrorBeam(Transform mirror, Vector3 forward, float length)
        {
            var start = mirror.position + forward * .12f;
            HotGameEntry.Sound.PlayTimelineCue(EnumSound.MirrorRay, start);
            var ray = CreateLine("金光 · 贯日", mirror, false);
            ray.sharedMaterial = rayMaterial;
            ray.positionCount = 2;
            ray.SetPosition(0, start);
            ray.SetPosition(1, mirror.position + forward * length);
            ray.sortingOrder = 9;
            return ray;
        }

        internal void MirrorImpact(Vector3 center)
        {
            for (int i = 0; i < 7; i++)
            {
                float a = i * 2.39996f;
                Vector3 end = center + new Vector3(Mathf.Cos(a), .45f + .12f * (i % 3), Mathf.Sin(a)) * (.33f + .09f * (i % 4));
                AddStroke(center, end, 0, .26f + .02f * i, .065f, new Color(1f, .77f, .29f));
            }
        }
    }
}
