using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGameFramework.Runtime;
using WorldTimeline;

// Presentation only. Never consumes combat RNG, advances simulation, or modifies actors.
public sealed class ExpeditionFormationVfx : IDisposable
{
    private readonly GameObject root;
    private readonly MeshRenderer[] rings = new MeshRenderer[6];
    private readonly Material ringMaterial, particleMaterial;
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    private readonly ParticleSystem motes;
    private int activeCell = -1;
    private static readonly int Tint = Shader.PropertyToID("_Color"), Selected = Shader.PropertyToID("_Selected");

    public ExpeditionFormationVfx(bool first)
    {
        root = new GameObject("布阵 · 青玉阵纹");
        var shader = Resources.Load<Shader>("FormationInk");
        if (shader == null) throw new InvalidOperationException("FormationInk shader is missing.");
        ringMaterial = new Material(shader) { name = "布阵地纹" };
        particleMaterial = new Material(shader) { name = "布阵玉屑" };
        particleMaterial.SetFloat("_Particle", 1);
        particleMaterial.SetColor(Tint, Color.white);
        for (int i = 0; i < rings.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "FormationRing" + i; go.transform.SetParent(root.transform, false);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            var cell = ExpeditionBattleSetup.Position(i, first);
            go.transform.position = HotGameEntry.HeroManager.GetTerrainMatchHeight(HotGameEntry.HeroManager.GetGeziPos(cell.y, cell.x)) + Vector3.up*.035f;
            go.transform.rotation = Quaternion.Euler(90,0,0); go.transform.localScale = Vector3.one*1.5f;
            rings[i] = go.GetComponent<MeshRenderer>(); rings[i].sharedMaterial = ringMaterial;
            rings[i].shadowCastingMode = ShadowCastingMode.Off; rings[i].receiveShadows = false;
        }
        var particles = new GameObject("SelectionMotes"); particles.transform.SetParent(root.transform, false);
        particles.transform.rotation = Quaternion.Euler(-90,0,0);
        motes = particles.AddComponent<ParticleSystem>(); motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = motes.main; main.playOnAwake=false; main.useUnscaledTime=true; main.loop=true;
        main.maxParticles=24; main.startLifetime=new ParticleSystem.MinMaxCurve(.8f,1.4f);
        main.startSize=new ParticleSystem.MinMaxCurve(.025f,.055f); main.startSpeed=new ParticleSystem.MinMaxCurve(.12f,.28f);
        main.simulationSpace=ParticleSystemSimulationSpace.Local;
        main.startColor=new Color(.70f,.94f,.82f,.75f);
        var emission=motes.emission; emission.rateOverTime=9;
        var shape=motes.shape; shape.shapeType=ParticleSystemShapeType.Circle; shape.radius=.57f; shape.radiusThickness=.12f;
        var fade=motes.colorOverLifetime; fade.enabled=true;
        fade.color=new Gradient { colorKeys=new[] {new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
            alphaKeys=new[] {new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.2f),new GradientAlphaKey(0,1)} };
        var size=motes.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,
            new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.3f,1),new Keyframe(1,0)));
        var renderer=particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=particleMaterial;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
    }

    public void Refresh(ExpeditionParty party, int selected, int hover = -1)
    {
        int cell=party.Owns(selected)?party.CellOf(selected):-1;
        for(int i=0;i<rings.Length;i++)
        {
            bool highlighted=i==cell || i==hover;
            properties.SetColor(Tint,highlighted?new Color(.54f,.92f,.76f,.95f):new Color(.25f,.40f,.32f,.72f));
            properties.SetFloat(Selected,highlighted?1:0); rings[i].SetPropertyBlock(properties);
        }
        if(activeCell==cell)return;
        activeCell=cell; motes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(cell>=0) { motes.transform.position=rings[cell].transform.position; motes.Play(); }
    }
    public void Dispose()
    {
        if(root!=null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        if(ringMaterial!=null)UnityEngine.Object.Destroy(ringMaterial);
        if(particleMaterial!=null)UnityEngine.Object.Destroy(particleMaterial);
    }
}
