using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.IO;

public class quadScript : MonoBehaviour {

    Slice[] _slices;
    ushort[] _pixelsCurrent = new ushort[0], _pixelsNext = new ushort[0];
    int _numSlices, _minIntensity, _maxIntensity, _xdim, _ydim, _zdim, _step = 4;

    float _iso = 1000f;

    List<Vector3> _vertices = new();
    List<int> _indices = new(); 

    private Button _buttonDraw, _buttonSave;
    private Slider _sliderIso, _sliderStepsize, _sliderLayer;

    meshScript _mscript;
    
    Texture2D _texture;
    // Use this for initialization
    void Start () 
    {
        var uiDocument = GameObject.Find("MyUIDocument").GetComponent<UIDocument>();
        _buttonDraw = uiDocument.rootVisualElement.Q("buttonDraw") as Button;
        _buttonSave = uiDocument.rootVisualElement.Q("buttonSave") as Button;
        _sliderIso = uiDocument.rootVisualElement.Q("sliderIso") as Slider;
        _sliderStepsize = uiDocument.rootVisualElement.Q("sliderStepsize") as Slider;
        _sliderLayer = uiDocument.rootVisualElement.Q("sliderLayer") as Slider;
        _buttonDraw.RegisterCallback<ClickEvent>(buttonDrawPushed);
        _buttonSave.RegisterCallback<ClickEvent>(buttonSavePushed);
        _sliderIso.RegisterValueChangedCallback(slicePosSliderIsoChange);
        _sliderStepsize.RegisterValueChangedCallback(slicePosSliderStepsizeChange);
        _sliderLayer.RegisterValueChangedCallback(slicePosSliderLayerChange);
        _texture = new Texture2D(512, 512, TextureFormat.RGB24, false);

        Slice.initDicom();

        string dicomfilepath = Application.dataPath + @"\..\dicomdata\"; // Application.dataPath is in the assets folder, but these files are "managed", so we go one level up
   
        _slices = processSlices(dicomfilepath);     // loads slices from the folder above
        _xdim = _slices[0].sliceInfo.Rows;
        _ydim = _slices[0].sliceInfo.Columns;
        _zdim = _slices.Length;  
        _pixelsNext = _slices[0].getPixels();     

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
                float val = pixelval(new Vector2(x, y), pixels);
                float v = (val-_minIntensity) / _maxIntensity;
                _texture.SetPixel(x, y, new UnityEngine.Color(v, v, v));
            }

        _texture.filterMode = FilterMode.Point; 
        _texture.Apply();
        GetComponent<Renderer>().material.mainTexture = _texture;
    }
 
    ushort pixelval(Vector2 p, ushort[] pixels)
    {
        return pixels[(int) p.x + (int) p.y * _xdim];
    }

    Vector4 GetPointAndValue(int x, int y, int z, ushort[] pixels)
    {
        Vector4 p = new(x,y,z);
        if (pixels.Length == 0) return p;
        if (x < 0 || y < 0 || x >= _xdim || y >= _ydim) return p;
        p.w = pixels[x+y*_xdim];
        return p;
    }

    void CreateMesh() 
    {
        _vertices.Clear();
        _indices.Clear();
        for (int z = -_step; z < _zdim + _step; z += _step)
        {   
            if (z >= 0 && z + _step < _zdim)
            {
                _pixelsCurrent = _pixelsNext;
                _pixelsNext = _slices[z+_step].getPixels();
            }
            if (z + _step > _zdim) _pixelsNext = new ushort[0];

            for (int x = -_step; x < _xdim + _step; x += _step)        
                for (int y = -_step; y < _ydim + _step; y += _step)        
                    {   
                        Vector4 p0 = GetPointAndValue(x, y+_step, z, _pixelsCurrent);
                        Vector4 p1 = GetPointAndValue(x+_step, y+_step, z, _pixelsCurrent);
                        Vector4 p2 = GetPointAndValue(x, y, z, _pixelsCurrent);
                        Vector4 p3 = GetPointAndValue(x+_step, y, z, _pixelsCurrent);
                        Vector4 p4 = GetPointAndValue(x, y+_step, z+_step, _pixelsNext);
                        Vector4 p5 = GetPointAndValue(x+_step, y+_step, z+_step, _pixelsNext);
                        Vector4 p6 = GetPointAndValue(x, y, z+_step, _pixelsNext);
                        Vector4 p7 = GetPointAndValue(x+_step, y, z+_step, _pixelsNext);

                        DoTetras(p4, p6, p0, p7);
                        DoTetras(p6, p0, p7, p2);
                        DoTetras(p0, p7, p2, p3);
                        DoTetras(p4, p5, p7, p0);
                        DoTetras(p1, p7, p0, p3);
                        DoTetras(p0, p5, p7, p1);
                    }
        }
    }

    void DoTetras(Vector4 v1, Vector4 v2, Vector4 v3, Vector4 v4)
    {
        string pattern = (v1.w < _iso ? "1" : "0") + (v2.w < _iso ? "1" : "0") + (v3.w < _iso ? "1" : "0") + (v4.w < _iso ? "1" : "0");
        
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

    Vector3 Interpolate(Vector4 a, Vector4 b)
    {
        float weight = Mathf.Abs(_iso - a.w) / Mathf.Abs(a.w-b.w);
        return Vector3.Lerp(a,b,weight);
    }

    void AddTriangle(Vector4 a, Vector4 b, Vector4 c, Vector4 d, Vector4 e, Vector4 f)
    {
        _vertices.Add(Normalize(Interpolate(a,b)));
        _vertices.Add(Normalize(Interpolate(c,d)));
        _vertices.Add(Normalize(Interpolate(e,f)));
        _indices.Add(_vertices.Count - 3);
        _indices.Add(_vertices.Count - 2);
        _indices.Add(_vertices.Count - 1);
    }

    void AddTriangle(Vector4 a, Vector4 b, Vector4 c, Vector4 d, Vector4 e, Vector4 f, Vector4 g, Vector4 h)
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