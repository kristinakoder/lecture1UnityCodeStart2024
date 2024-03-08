using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Diagnostics;

public class quadScript : MonoBehaviour {

    // Dicom har et "levende" dictionary som leses fra xml ved initDicom
    // slices må sorteres, og det basert på en tag, men at pixeldata lesing er en separat operasjon, derfor har vi nullpeker til pixeldata
    // dicomfile lagres slik at fil ikke må leses enda en gang når pixeldata hentes
    
    // member variables of quadScript, accessible from any function
    Slice[] _slices;
    int _numSlices, _minIntensity, _maxIntensity, _xdim, _ydim, _zdim, _step = 4;

    float _iso = 0.8f;
    bool checkedToggle = true;

    List<Vector3> _vertices = new();
    List<int> _indices = new(); 

    private Button _buttonDraw, _buttonSave;
    private Toggle _toggle;
    private Slider _sliderIso, _sliderStepsize, _sliderLayer;

    meshScript _mscript;
    
    Texture2D _texture;
    // Use this for initialization
    void Start () 
    {
        var uiDocument = GameObject.Find("MyUIDocument").GetComponent<UIDocument>();
        _buttonDraw = uiDocument.rootVisualElement.Q("buttonDraw") as Button;
        _buttonSave = uiDocument.rootVisualElement.Q("buttonSave") as Button;
        _toggle = uiDocument.rootVisualElement.Q("toggle1") as Toggle;
        _sliderIso = uiDocument.rootVisualElement.Q("sliderIso") as Slider;
        _sliderStepsize = uiDocument.rootVisualElement.Q("sliderStepsize") as Slider;
        _sliderLayer = uiDocument.rootVisualElement.Q("sliderLayer") as Slider;
        _buttonDraw.RegisterCallback<ClickEvent>(buttonDrawPushed);
        _buttonSave.RegisterCallback<ClickEvent>(buttonSavePushed);
        _sliderIso.RegisterValueChangedCallback(slicePosSliderIsoChange);
        _sliderStepsize.RegisterValueChangedCallback(slicePosSliderStepsizeChange);
        _sliderLayer.RegisterValueChangedCallback(slicePosSliderLayerChange);
        _toggle.RegisterValueChangedCallback(OnToggleValueChanged);
        _texture = new Texture2D(512, 512, TextureFormat.RGB24, false);

        Slice.initDicom();

        string dicomfilepath = Application.dataPath + @"\..\dicomdata\"; // Application.dataPath is in the assets folder, but these files are "managed", so we go one level up
   
        _slices = processSlices(dicomfilepath);     // loads slices from the folder above
        _xdim = _slices[0].sliceInfo.Rows;
        _ydim = _slices[0].sliceInfo.Columns;
        _zdim = _slices.Length;           

        _mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
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

        _minIntensity = (int)min;
        _maxIntensity = (int)max;

        Array.Sort(slices);
        
        return slices;
    }

    void setTexture(Slice slice)
    { 
        ushort[] pixels = slice.getPixels();
        
        for (int y = 0; y < _ydim; y++)
            for (int x = 0; x < _xdim; x++)
            {
                float val = pixelval(new Vector2(x, y), _xdim, pixels);
                float v = (val-_minIntensity) / _maxIntensity;      // maps [_minIntensity,_maxIntensity] to [0,1] , i.e.  _minIntensity to black and _maxIntensity to white
                _texture.SetPixel(x, y, new UnityEngine.Color(v, v, v));
            }

        _texture.filterMode = FilterMode.Point;  // nearest neigbor interpolation is used.  (alternative is FilterMode.Bilinear)
        _texture.Apply();  // Apply all SetPixel calls
        GetComponent<Renderer>().material.mainTexture = _texture;
    }
 
    ushort pixelval(Vector2 p, int xdim, ushort[] pixels)
    {
        return pixels[(int)p.x + (int)p.y * xdim];
    }

    void CreateMesh() 
    {
        _vertices.Clear();
        _indices.Clear();
        for (int x = -_step; x < _xdim + _step; x += _step)        
            for (int y = -_step; y < _ydim + _step; y += _step)        
                for (int z = -_step; z < _zdim + _step; z += _step)          
                {   
                    Vector3 p0 = new(x,y+_step,z);
                    Vector3 p1 = new(x+_step, y+_step, z);
                    Vector3 p2 = new(x,y,z);
                    Vector3 p3 = new(x+_step, y, z);
                    Vector3 p4 = new(x, y+_step, z+_step);
                    Vector3 p5 = new(x+_step, y+_step, z+_step);
                    Vector3 p6 = new(x, y, z+_step);
                    Vector3 p7 = new(x+_step, y, z+_step);

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
        if (v.x < 0 || v.y < 0 || v.z < 0 || v.z > 352 || v.x > 511 || v.y > 511) return 0;
        ushort[] pixels = _slices[(int) v.z].getPixels(); //denne bør plasseres et annet sted
        int pos = (int)v.x + (int)v.y * _xdim;
        if (pos < 0 || pos > pixels.Length) return 0;
        return pixels[pos];
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e, Vector3 f)
    {
        _vertices.Add(Normalize(Interpolate(a,b)));
        _vertices.Add(Normalize(Interpolate(c,d)));
        _vertices.Add(Normalize(Interpolate(e,f)));
        _indices.Add(_vertices.Count - 3);
        _indices.Add(_vertices.Count - 2);
        _indices.Add(_vertices.Count - 1);
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e, Vector3 f, Vector3 g, Vector3 h)
    {
        AddTriangle(a, b, c, d, e, f);
        AddTriangle(a, b, e, f, g, h);  
    }

    Vector3 Normalize(Vector3 a)
    {
        a.z *= (float) _xdim/_zdim;
        return a/_xdim - Vec3(0.5f, 0.5f, 0.5f);
    }

    Vector3 Vec3(float x, float y, float z)
    {
        return new Vector3(x, y, z);
    }

    private void OnToggleValueChanged(ChangeEvent<bool> evt)
    {
        checkedToggle = evt.newValue;
        print("toggle: " + checkedToggle);
    }
       
    public void slicePosSliderIsoChange(ChangeEvent<float> evt)
    {
        _sliderIso.value = evt.newValue;
        _iso = evt.newValue;
    }    
    
    public void slicePosSliderStepsizeChange(ChangeEvent<float> evt)
    {
        _sliderStepsize.value = evt.newValue;
        _step = (int) _sliderStepsize.value;
    }
    
    private void slicePosSliderLayerChange(ChangeEvent<float> evt)
    {
        int n = (int) evt.newValue;
        setTexture(_slices[n]);
    }
    
    public void buttonDrawPushed(ClickEvent evt)
    {
        CreateMesh(); 
        _mscript.createMeshGeometry(_vertices, _indices);  
    }

    public void buttonSavePushed(ClickEvent evt)
    {
        CreateMesh(); 
        _mscript.MeshToFile("test.obj", ref _vertices, ref _indices);
    }
}