// Procedural Fairings plug-in by Alexey Volynskov
// Licensed under CC BY 3.0 terms: http://creativecommons.org/licenses/by/3.0/
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;


namespace Keramzit {


//ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ//


struct BezierSlope
{
  Vector2 p1, p2;

  public BezierSlope(Vector4 v)
  {
    p1=new Vector2(v.x, v.y);
    p2=new Vector2(v.z, v.w);
  }

  public Vector2 interp(float t)
  {
    Vector2 a=Vector2.Lerp(Vector2.zero, p1, t);
    Vector2 b=Vector2.Lerp(p1, p2, t);
    Vector2 c=Vector2.Lerp(p2, Vector2.one, t);

    Vector2 d=Vector2.Lerp(a, b, t);
    Vector2 e=Vector2.Lerp(b, c, t);

    return Vector2.Lerp(d, e, t);
  }
}


//ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ//

    internal class Tuple<T1, T2>
    {
        internal T1 Item1 { get; set; }
        internal T2 Item2 { get; set; }

        public Tuple(T1 item1, T2 item2)
        {
            this.Item1 = item1;
            this.Item2 = item2;
        }
    }

//ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ//

public static class PFUtils
{
  public static bool canCheckTech()
  {
    return HighLogic.LoadedSceneIsEditor &&
      (ResearchAndDevelopment.Instance!=null || (HighLogic.CurrentGame.Mode!=Game.Modes.CAREER && HighLogic.CurrentGame.Mode!=Game.Modes.SCIENCE_SANDBOX));
  }


  public static bool haveTech(string name)
  {
    if (HighLogic.CurrentGame.Mode!=Game.Modes.CAREER && HighLogic.CurrentGame.Mode!=Game.Modes.SCIENCE_SANDBOX) return name=="sandbox";
    return ResearchAndDevelopment.GetTechnologyState(name)==RDTech.State.Available;
  }


  public static float getTechMinValue(string cfgname, float defVal)
  {
    bool hasValue=false;
    float minVal=0;

    foreach (var tech in GameDatabase.Instance.GetConfigNodes(cfgname))
      for (int i=0; i<tech.values.Count; ++i)
      {
        var value=tech.values[i];
        if (!haveTech(value.name)) continue;
        float v=float.Parse(value.value);
        if (!hasValue || v<minVal) { minVal=v; hasValue=true; }
      }

    if (!hasValue) return defVal;
    return minVal;
  }


  public static float getTechMaxValue(string cfgname, float defVal)
  {
    bool hasValue=false;
    float maxVal=0;

    foreach (var tech in GameDatabase.Instance.GetConfigNodes(cfgname))
      for (int i=0; i<tech.values.Count; ++i)
      {
        var value=tech.values[i];
        if (!haveTech(value.name)) continue;
        float v=float.Parse(value.value);
        if (!hasValue || v>maxVal) { maxVal=v; hasValue=true; }
      }

    if (!hasValue) return defVal;
    return maxVal;
  }


  public static void setFieldRange(BaseField field, float minval, float maxval)
  {
    var fr=field.uiControlEditor as UI_FloatRange;
    if (fr!=null)
    {
      fr.minValue=minval;
      fr.maxValue=maxval;
    }

    var fe=field.uiControlEditor as UI_FloatEdit;
    if (fe!=null)
    {
      fe.minValue=minval;
      fe.maxValue=maxval;
    }
  }


  public static void updateAttachedPartPos(AttachNode node, Part part)
  {
    if (node==null || part==null) return;

    var ap=node.attachedPart;
    if (!ap) return;

    var an=ap.findAttachNodeByPart(part);
    if (an==null) return;

    var dp=
      part.transform.TransformPoint(node.position)-
      ap.transform.TransformPoint(an.position);

    if (ap==part.parent)
    {
      while (ap.parent) ap=ap.parent;
      ap.transform.position+=dp;
      part.transform.position-=dp;
    }
    else
      ap.transform.position+=dp;
  }


  public static string formatMass(float mass)
  {
    if (mass<0.01f) return (mass*1e3f).ToString("n3")+"kg";
    else return mass.ToString("n3")+"t";
  }

  public static string formatCost(float cost)
  {
    return cost.ToString("n0");
  }

  public static void enableRenderer(Transform t, bool e)
  {
    if (!t) return;
    var r=t.GetComponent<Renderer>();
    if (r) r.enabled=e;
  }

  public static void hideDragStuff(Part part)
  {
    enableRenderer(part.FindModelTransform("dragOnly"), false);
  }

  public static bool FARinstalled=false, FARchecked=false;

  public static bool isFarInstalled()
  {
    if (!FARchecked)
    {
      FARinstalled=AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name=="FerramAerospaceResearch");
      FARchecked=true;
    }
    return FARinstalled;
  }

  public static void updateDragCube(Part part, float areaScale)
  {
    if (isFarInstalled())
    {
      // Debug.Log("calling FAR to update voxels");
      part.SendMessage("GeometryPartModuleRebuildMeshData");
    }

    if (!HighLogic.LoadedSceneIsFlight) return;

    enableRenderer(part.FindModelTransform("dragOnly"), true);

    var dragCube=DragCubeSystem.Instance.RenderProceduralDragCube(part);

    enableRenderer(part.FindModelTransform("dragOnly"), false);

    for (int i=0; i<6; ++i) dragCube.Area[i]*=areaScale;
    // dragCube.Area[(int)DragCube.DragFace.YP]*=areaScale;
    // dragCube.Area[(int)DragCube.DragFace.YN]*=areaScale;

    // Debug.Log(part.name+" dragCube area="+dragCube.Area[(int)DragCube.DragFace.YP]+" size="+dragCube.Size+" ms="+model.localScale);
    part.DragCubes.ClearCubes();
    part.DragCubes.Cubes.Add(dragCube);
    part.DragCubes.ResetCubeWeights();
  }

  public static IEnumerator<YieldInstruction> updateDragCubeCoroutine(Part part, float areaScale)
  {
    while (true)
    {
      if (part==null || part.Equals(null)) yield break;

      if (HighLogic.LoadedSceneIsFlight)
      {
        if (part.vessel==null || part.vessel.Equals(null)) yield break;

        if (!FlightGlobals.ready || part.packed || !part.vessel.loaded)
        {
          yield return new WaitForFixedUpdate();
          continue;
        }
        break;
      }
      else if (HighLogic.LoadedSceneIsEditor)
      {
        yield return new WaitForFixedUpdate();
        break;
      }
      else
        yield break;
    }

    PFUtils.updateDragCube(part, areaScale);
  }

  public static void refreshPartWindow()
  {
    foreach (var w in UnityEngine.Object.FindObjectsOfType<UIPartActionWindow>()) w.displayDirty=true;
  }

  public static Part partFromHit(this RaycastHit hit)
  {
      if (hit.collider == null || hit.collider.gameObject == null)
      {
          return null;
      }
      var go = hit.collider.gameObject;
      var p = Part.FromGO(go);
      while (p == null)
      {
          if (go.transform != null && go.transform.parent != null && go.transform.parent.gameObject != null)
          {
              go = go.transform.parent.gameObject;
          }
          else
          {
              break;
          }
          p = Part.FromGO(go);
      }
      return p;
  }

  public static List<Part> getAllChildrenRecursive(this Part rootPart, bool root)
  {
      var children = new List<Part>();
      if (!root)
      {
          children.Add(rootPart);
      }
      foreach (var child in rootPart.children)
      {
          children.AddRange(child.getAllChildrenRecursive(false));
      }
      return children;
  }
}


//ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ//


[KSPAddon(KSPAddon.Startup.EditorAny, false)]
public class EditorScreenMessager : MonoBehaviour
{
  static float osdMessageTime=0;
  static string osdMessageText=null;

  public static void showMessage(string msg, float delay)
  {
    osdMessageText=msg;
    osdMessageTime=Time.time+delay;
  }

  public void OnGUI()
  {
    if (!HighLogic.LoadedSceneIsEditor) return;

    if (Time.time<osdMessageTime)
    {
      GUI.skin=HighLogic.Skin;
      GUIStyle style=new GUIStyle("Label");
      style.alignment=TextAnchor.MiddleCenter;
      style.fontSize=20;
      style.normal.textColor=Color.black;
      GUI.Label(new Rect(2, 2+(Screen.height/9), Screen.width, 50), osdMessageText, style);
      style.normal.textColor=Color.yellow;
      GUI.Label(new Rect(0, Screen.height/9, Screen.width, 50), osdMessageText, style);
    }
  }
}


//ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ//


} // namespace
