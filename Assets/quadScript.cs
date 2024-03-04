using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Data;

public class quadScript : MonoBehaviour {

    // Dicom har et "levende" dictionary som leses fra xml ved initDicom
    // slices må sorteres, og det basert på en tag, men at pixeldata lesing er en separat operasjon, derfor har vi nullpeker til pixeldata
    // dicomfile lagres slik at fil ikke må leses enda en gang når pixeldata hentes
    
    // member variables of quadScript, accessible from any function
    Slice[] _slices;
    int _numSlices, _minIntensity, _maxIntensity, xdim, ydim, zdim;

    float _iso = 0.8f;
    bool checkedToggle = true;

    int _radius = 20, divitions = 30;

    List<Vector3> vertices = new();
    List<int> indices = new(); 

    private Button _button, _button2;
    private Toggle _toggle;
    private Slider _slider1, _slider2, _sliderRed;

    meshScript mscript;
    
    Texture2D texture;
    // Use this for initialization
    void Start () 
    {
        var uiDocument = GameObject.Find("MyUIDocument").GetComponent<UIDocument>();
        _button = uiDocument.rootVisualElement.Q("button1") as Button;
        _button2 = uiDocument.rootVisualElement.Q("button2") as Button;
        _toggle = uiDocument.rootVisualElement.Q("toggle1") as Toggle;
        _slider1 = uiDocument.rootVisualElement.Q("slider1") as Slider;
        _slider2 = uiDocument.rootVisualElement.Q("slider2") as Slider;
        _sliderRed = uiDocument.rootVisualElement.Q("sliderR") as Slider;
        _button.RegisterCallback<ClickEvent>(button1Pushed);
        _button2.RegisterCallback<ClickEvent>(button2Pushed);
        _slider1.RegisterValueChangedCallback(slicePosSlider1Change);
        _slider2.RegisterValueChangedCallback(slicePosSlider2Change);
        _sliderRed.RegisterValueChangedCallback(slicePosSliderRedChange);
        _toggle.RegisterValueChangedCallback(OnToggleValueChanged);
        texture = new Texture2D(512, 512, TextureFormat.RGB24, false);

        Slice.initDicom();

        string dicomfilepath = Application.dataPath + @"\..\dicomdata\"; // Application.dataPath is in the assets folder, but these files are "managed", so we go one level up
   
        _slices = processSlices(dicomfilepath);     // loads slices from the folder above
        xdim = _slices[0].sliceInfo.Rows;
        ydim = _slices[0].sliceInfo.Columns;
        zdim = _slices.Length;           

        mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
    }

    Slice[] processSlices(string dicomfilepath)
    {
        string[] dicomfilenames = Directory.GetFiles(dicomfilepath, "*.IMA"); 
        _numSlices =  dicomfilenames.Length;

        Slice[] slices = new Slice[_numSlices];

        float max = -1;
        float min = 99999;
        for (int i = 0; i < _numSlices; i++)
        {
            string filename = dicomfilenames[i];
            slices[i] = new Slice(filename);
            SliceInfo info = slices[i].sliceInfo;
            if (info.LargestImagePixelValue > max) max = info.LargestImagePixelValue;
            if (info.SmallestImagePixelValue < min) min = info.SmallestImagePixelValue;
            // Del dataen på max før den settes inn i tekstur
            // alternativet er å dele på 2^dicombitdepth,  men det ville blitt 4096 i dette tilfelle

        }
        print("Number of slices read:" + _numSlices);
        print("Max intensity in all slices:" + max);
        print("Min intensity in all slices:" + min);

        _minIntensity = (int)min;
        _maxIntensity = (int)max;

        Array.Sort(slices);
        
        return slices;
    }

    void setTexture(Slice slice)
    { 
        ushort[] pixels = slice.getPixels();
        
        for (int y = 0; y < ydim; y++)
            for (int x = 0; x < xdim; x++)
            {
                float val = pixelval(new Vector2(x, y), xdim, pixels);
                float v = (val-_minIntensity) / _maxIntensity;      // maps [_minIntensity,_maxIntensity] to [0,1] , i.e.  _minIntensity to black and _maxIntensity to white
                texture.SetPixel(x, y, new UnityEngine.Color(v, v, v));
            }

        texture.filterMode = FilterMode.Point;  // nearest neigbor interpolation is used.  (alternative is FilterMode.Bilinear)
        texture.Apply();  // Apply all SetPixel calls
        GetComponent<Renderer>().material.mainTexture = texture;
    }
 
    ushort pixelval(Vector2 p, int xdim, ushort[] pixels)
    {
        return pixels[(int)p.x + (int)p.y * xdim];
    }

    void DrawFilledCircle(float r, float g, float b) 
    {                
        for (int y = 0; y < ydim; y++)
            for (int x = 0; x < xdim; x++)
            {
                texture.SetPixel(x, y, new UnityEngine.Color(0, 0, 0));
                if (IsInside(new Vector3(x, y))) texture.SetPixel(x, y, new UnityEngine.Color(r, g, b));
            }

        texture.filterMode = FilterMode.Point;  // nearest neigbor interpolation is used.  (alternative is FilterMode.Bilinear)
        texture.Apply();  // Apply all SetPixel calls
        GetComponent<Renderer>().material.mainTexture = texture;
    }

    void DrawByPoints() 
    {
        vertices.Clear();
        indices.Clear();
        float step = (float) xdim/divitions;
        float half = step/2;
        for (float x = half; x < xdim; x += step)        
            for (float y = half; y < ydim; y += step)
                {
                Vector3 v1 = Vec3(x,y);
                Vector3 v2 = Vec3(x + step, y);
                Vector3 v3 = Vec3(x + step, y + step);
                Vector3 v4 = Vec3(x, y + step);

                string pattern = (IsInside(v1) ? "1" : "0") + (IsInside(v2) ? "1" : "0") + (IsInside(v3) ? "1" : "0") + (IsInside(v4) ? "1" : "0");

                switch (pattern)
                {
                    case "1110": case "0001":  //{ true, true, true, false }                  
                        AddVertexAndIndices(x, y+half, x+half, y+step);
                        break;
                    case "1101": case "0010": //{ true, true, false, true }
                        AddVertexAndIndices(x+step, y+half, x+half, y+step);
                        break;
                    case "1100": case "0011": // { true, true, false, false }
                         AddVertexAndIndices(x, y+half, x+step, y+half);
                         break;
                    case "0111": case "1000": //{ false, true, true, true }
                        AddVertexAndIndices(x+half, y, x, y+half);
                        break;
                    case "0110": case "1001": //{ false, true, true, false }
                        AddVertexAndIndices(x+half, y, x+half, y+step);
                        break;
                    case "0100": case "1011": //{ false, true, false, false }
                        AddVertexAndIndices(x+half, y, x+step, y+half);
                        break;
                    default:
                        break;
                }
            } 
        mscript.createMeshGeometry(vertices, indices);
    }

    void CreateMesh() 
    {
        vertices.Clear();
        indices.Clear();
        float zstep = (float) zdim/divitions;
        float zhalf = (float) zstep/2;
        float step = (float) xdim/divitions;
        float half = (float) step/2;
        for (float x = - half; x < xdim + step; x += step)        
            for (float y = - half; y < ydim + step; y += step)        
                for (float z = - zhalf; z < zdim + zstep; z += zstep)          
                {   
                    Vector3 p0 = new(x,y+step,z);
                    Vector3 p1 = new(x+step, y+step, z);
                    Vector3 p2 = new(x,y,z);
                    Vector3 p3 = new(x+step, y, z);
                    Vector3 p4 = new(x, y+step, z+zstep);
                    Vector3 p5 = new(x+step, y+step, z+zstep);
                    Vector3 p6 = new(x, y, z+zstep);
                    Vector3 p7 = new(x+step, y, z+zstep);

                    DoTetras(p4, p6, p0, p7);
                    DoTetras(p6, p0, p7, p2);
                    DoTetras(p0, p7, p2, p3);
                    DoTetras(p4, p5, p7, p0);
                    DoTetras(p1, p7, p0, p3);
                    DoTetras(p0, p5, p7, p1);
                }
        
    }

    void DoTetras(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
    {
        string pattern = (UnderIso(v1) ? "1" : "0") + (UnderIso(v2) ? "1" : "0") + (UnderIso(v3) ? "1" : "0") + (UnderIso(v4) ? "1" : "0");
        
        switch(pattern)
        {
            case "0001": //p14, p24, p34   
                AddTriangle(v1, v4, v2, v4, v3, v4); 
                break;
            case "1110": //p14, p34, p24               
                AddTriangle(v1, v4, v3, v4, v2, v4);
                break;
            case "0010": //p13, p34, p23
                AddTriangle(v1, v3, v3, v4, v2, v3);
                break; 
            case "1101": //p13, p23, p34         
                AddTriangle(v1, v3, v2, v3, v3, v4);
                break;
            case "0100": //p12, p23, p24
                AddTriangle(v1, v2, v2, v3, v2, v4);
                break; 
            case "1011": // p12, p24, p23
                AddTriangle(v1, v2, v2, v4, v2, v3);
                break;
            case "1000": //p12, p14, p13
                AddTriangle(v1, v2, v1, v4, v1, v3);
                break;
            case "0111": //p12, p13, p14
                AddTriangle(v1, v2, v1, v3, v1, v4);
                break;
            case "0011": //p13, p14, p24, p23
                AddTriangle(v1, v3, v1, v4, v2, v4, v2, v3);
                break;
            case "1100": //p13, p23, p24, p14
                AddTriangle(v1, v3, v2, v3, v2, v4, v1, v4);
                break;
            case "1010": //p12, p14, p34, p23
                AddTriangle(v1, v2, v1, v4, v3, v4, v2, v3);
                break;
            case "0101": //p12, p23, p34, p14
                AddTriangle(v1, v2, v2, v3, v3, v4, v1, v4);
                break;
            case "0110": //p12, p13, p34, p24
                AddTriangle(v1, v2, v1, v3, v3, v4, v2, v4);
                break;
            case "1001": //p12, p24, p34, p13
                AddTriangle(v1, v2, v2, v4, v3, v4, v1, v3);
                break;
            default:
                break;
        }
    }

    Vector3 Interpolate(Vector3 a, Vector3 b)
    {
        float v1 = FindIso(a), v2 = FindIso(b);
        float weight = Mathf.Abs(_iso - v1) / Mathf.Abs(v1-v2);
        return Vector3.Lerp(a,b,weight);
    }

    bool UnderIso(Vector3 v)
    {
        return FindIso(v) < _iso;
    }

    float FindIso(Vector3 v)
    {
        if (v.x < 0 || v.y < 0 || v.z < 0 || v.z > 354 || v.x > 512 || v.y > 512) return 0;
        ushort[] pixels = _slices[(int) v.z].getPixels(); //føles litt teit at denne skal kalles unødvendig mange ganger
        int pos = (int)v.x + (int)v.y * xdim;
        return pixels[pos];
    }

    bool MatchPattern(bool[] a, bool[] b)
    {
        return a.SequenceEqual(b) || a.SequenceEqual(b.Select(b => !b).ToArray());
    }
    
    bool IsInside(Vector3 v)
    {
        return FindDistance(v) < _radius;
    }

    float FindDistance(Vector3 v)
    {
        Vector3 center = new(xdim/2,xdim/2,xdim/2);
        return Vector3.Distance(v, center); //Vector3.Distance(new Vector2(x,y), center) denne verdier skal brukes mot _iso
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e, Vector3 f)
    {
        vertices.Add(Normalize(Interpolate(a,b)));
        vertices.Add(Normalize(Interpolate(c,d)));
        vertices.Add(Normalize(Interpolate(e,f)));
        indices.Add(vertices.Count - 3);
        indices.Add(vertices.Count - 2);
        indices.Add(vertices.Count - 1);
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e, Vector3 f, Vector3 g, Vector3 h)
    {
        AddTriangle(a, b, c, d, e, f);
        AddTriangle(a, b, e, f, g, h);  
    }

    Vector3 Normalize(Vector3 a)
    {
        a.z *= (float) xdim/zdim;
        return a/xdim - Vec3(0.5f, 0.5f, 0.5f);
    }

    Vector3 Vec3(float x, float y, float z)
    {
        return new Vector3(x, y, z);
    }

    Vector3 Vec3(float x, float y)
    {
        return new Vector3(x, y);
    }

    void AddVertexAndIndices(float a, float b, float c, float d)
    {
        Func<float, float> normalize = (x) => x / xdim - 0.5f;
        vertices.Add(Vec3(normalize(a),normalize(b)));
        vertices.Add(Vec3(normalize(c),normalize(d)));
        indices.Add(vertices.Count - 2);
        indices.Add(vertices.Count - 1);
    }

    private void OnToggleValueChanged(ChangeEvent<bool> evt)
    {
        checkedToggle = evt.newValue;
        print("toggle: " + checkedToggle);
    }
       
    public void slicePosSlider1Change(ChangeEvent<float> evt)
    {
        _slider1.value = evt.newValue;
        _iso = evt.newValue;
    }    
    
    public void slicePosSlider2Change(ChangeEvent<float> evt)
    {
        _slider2.value = evt.newValue;
        divitions = (int) _slider2.value;
    }
    
    private void slicePosSliderRedChange(ChangeEvent<float> evt)
    {
        int n = (int) evt.newValue;
        setTexture(_slices[n]);
    }
    
    public void button1Pushed(ClickEvent evt)
    {
        CreateMesh(); 
        mscript.createMeshGeometry(vertices, indices);
    }

    public void button2Pushed(ClickEvent evt)
    {
        CreateMesh(); 
        mscript.MeshToFile("test.obj", ref vertices, ref indices);
    }
}