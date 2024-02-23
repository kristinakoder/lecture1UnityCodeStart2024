﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine.Rendering;

public class meshScript : MonoBehaviour
{
    void Start()
    {
        // programatically create meshfilter and meshrenderer and add to gameobject this script is attached to.
        GameObject go = gameObject; // GameObject.Find("GameObjectDp");
        MeshFilter meshFilter = (MeshFilter)go.AddComponent(typeof(MeshFilter));
        MeshRenderer renderer = go.AddComponent(typeof(MeshRenderer)) as MeshRenderer;
        renderer.material = Resources.Load<Material>("New Material");
    }
 
    public void createMeshGeometry(List<Vector3> vertices, List<int> indices)
    {
        Mesh mesh = GetComponent<MeshFilter>().mesh;
		mesh.Clear();
  
        mesh.SetVertices(vertices);
        
        mesh.indexFormat = IndexFormat.UInt32;
        
        //mesh.SetUVs()
     
        // https://docs.unity3d.com/ScriptReference/MeshTopology.html
        // mesh.SetIndices(Triangles.ToArray(), MeshTopology.Points, 0);
        //mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
        // mesh.SetIndices(indices.ToArray(), MeshTopology.LineStrip, 0); 
        mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);

 
        // mesh.MarkDynamic();  // https://docs.unity3d.com/ScriptReference/Mesh.MarkDynamic.html
        // For iterative mesh additions without reloading the old mesh data   https://docs.unity3d.com/ScriptReference/Mesh.CombineMeshes.html
        //mesh.Optimize();  //https://docs.unity3d.com/ScriptReference/Mesh.Optimize.html
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }
    
    // the code below is for saving the mesh to file in .obj format (see https://en.wikipedia.org/wiki/Wavefront_.obj_file) which can be loaded by e.g. meshlab
    public void MeshToFile(string filename, ref List<Vector3> vertices, ref List<int> indices)
    {
        using (StreamWriter sw = new StreamWriter(filename))
            sw.Write(MeshToString(ref vertices, ref indices));
        print("Mesh saved to file: " + filename);
    }
  
    private string MeshToString( ref List<Vector3> vertices, ref List<int> triangles)
    {
        StringBuilder sb = new StringBuilder();
  
        sb.Append("g ").Append("mesh").Append("\n");
        foreach (Vector3 v in vertices)  
            sb.AppendFormat(CultureInfo.InvariantCulture, "v {0} {1} {2}\n", v.x, v.y, v.z);  //  sb.AppendFormat(string.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}\n", v.x, v.y, v.z));
        
        for (int i = 0; i < triangles.Count; i += 3)
            sb.AppendFormat(CultureInfo.InvariantCulture,"f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", triangles[i] + 1, triangles[i + 1] + 1,  triangles[i + 2] + 1);
   
        //sb.AppendFormat(string.Format(CultureInfo.InvariantCulture,"f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", triangles[i] + 1, triangles[i + 1] + 1,  triangles[i + 2] + 1));
  
        return sb.ToString();
    }
    
}