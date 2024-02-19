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
    int _numSlices, _minIntensity, _maxIntensity, xdim, ydim;

    float red = 1, green = 1, blue = 1, step, half;
    bool checkedToggle = true;

    int _iso = 20, divitions = 30;

    List<Vector3> vertices = new(); //3. tall er 0 alltid.
    List<int> indices = new(); //legger til 0 og 1, da tegnes kant fra punkt 0 til 1. Legger til 1 og 2, kant fra 1 til 2 osv...

    private Button _button, _button2;
    private Toggle _toggle;
    private Slider _slider1, _slider2, _sliderRed, _sliderGreen, _sliderBlue;

    meshScript mscript;
    

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
        _sliderGreen = uiDocument.rootVisualElement.Q("sliderG") as Slider;
        _sliderBlue = uiDocument.rootVisualElement.Q("sliderB") as Slider;
        _button.RegisterCallback<ClickEvent>(button1Pushed);
        _button2.RegisterCallback<ClickEvent>(button2Pushed);
        _slider1.RegisterValueChangedCallback(slicePosSlider1Change);
        _slider2.RegisterValueChangedCallback(slicePosSlider2Change);
        _sliderRed.RegisterValueChangedCallback(slicePosSliderRedChange);
        _sliderGreen.RegisterValueChangedCallback(slicePosSliderGreenChange);
        _sliderBlue.RegisterValueChangedCallback(slicePosSliderBlueChange);
        _toggle.RegisterValueChangedCallback(OnToggleValueChanged);

        Slice.initDicom();

        string dicomfilepath = Application.dataPath + @"\..\dicomdata\"; // Application.dataPath is in the assets folder, but these files are "managed", so we go one level up
   
        _slices = processSlices(dicomfilepath);     // loads slices from the folder above
        xdim = _slices[0].sliceInfo.Rows;
        ydim = _slices[0].sliceInfo.Columns;
        //DrawFilledCircle(red, green, blue);             

        //  gets the mesh object and uses it to create a diagonal line
        mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        //List<Vector3> vertices = new List<Vector3>();
        //List<int> indices = new List<int>();
        //vertices.Add(new Vector3(-0.5f,-0.5f,0));
        //vertices.Add(new Vector3(0.5f,0.5f,0));
        //indices.Add(0);
        //indices.Add(1);
        //mscript.createMeshGeometry(vertices, indices);
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
        //_iso = 0;

        Array.Sort(slices);
        
        return slices;
    }

    void setTexture(Slice slice)
    {        
        var texture = new Texture2D(xdim, ydim, TextureFormat.RGB24, false);     // garbage collector will tackle that it is new'ed 

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

    void DrawFilledCircle(float r, float g, float b) 
    {        
        var texture = new Texture2D(xdim, ydim, TextureFormat.RGB24, false); // garbage collector will tackle that it is new'ed 
        
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

    void DrawCircleByPoints() 
    {
        vertices.Clear();
        indices.Clear();
        step = (float) xdim/divitions;
        half = step/2;
        for (float x = half; x < xdim; x += step)        
            for (float y = half; y < ydim; y += step)
                {
                bool[] square = CheckSquares(x,y);
 
                if (MatchPattern(square, new bool[] { true, true, true, false }))
                    AddVertexAndIndices(x, y+half, x+half, y+step);
                
                if (MatchPattern(square, new bool[] { true, true, false, true }))
                    AddVertexAndIndices(x+step, y+half, x+half, y+step);

                if (MatchPattern(square, new bool[] { true, true, false, false }))
                    AddVertexAndIndices(x, y+half, x+step, y+half);

                if (MatchPattern(square, new bool[] { false, true, true, true })) 
                    AddVertexAndIndices(x+half, y, x, y+half);

                if (MatchPattern(square, new bool[] { false, true, true, false })) 
                    AddVertexAndIndices(x+half, y, x+half, y+step);
                
                if (MatchPattern(square, new bool[] { false, true, false, false })) 
                    AddVertexAndIndices(x+half, y, x+step, y+half);
            } 
        mscript.createMeshGeometry(vertices, indices);
    }

    bool[] CheckSquares(float x, float y)
    {
        return new bool[] { IsInside(Vec3(x, y)), IsInside(Vec3(x + step, y)), IsInside(Vec3(x + step, y + step)), IsInside(Vec3(x, y + step)) };
    }

    void DrawMesh() 
    {
        vertices.Clear();
        indices.Clear();
        step = (float) xdim/divitions;
        half = step/2;
        for (float x = half; x < xdim; x += step)        
            for (float y = half; y < ydim; y += step)
                for (float z = half; z < ydim; z += step)
                {   
                    Vector3 p0 = new(x,y+step,z);
                    Vector3 p1 = new(x+step, y+step, z);
                    Vector3 p2 = new(x,y,z);
                    Vector3 p3 = new(x+step, y, z);
                    Vector3 p4 = new(x, y+step, z+step);
                    Vector3 p5 = new(x+step, y+step, z+step);
                    Vector3 p6 = new(x, y, z+step);
                    Vector3 p7 = new(x+step, y, z+step);

                    CheckTetrahedras(p4, p6, p0, p7);
                    CheckTetrahedras(p6, p0, p7, p2);
                    CheckTetrahedras(p0, p7, p2, p3);
                    CheckTetrahedras(p4, p5, p7, p0);
                    CheckTetrahedras(p1, p7, p0, p3);
                    CheckTetrahedras(p0, p5, p7, p1);
                }
        mscript.createMeshGeometry(vertices, indices);
    }

    void CheckTetrahedras(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        string pattern = (IsInside(a) ? "1" : "0") + (IsInside(b) ? "1" : "0") + (IsInside(c) ? "1" : "0") + (IsInside(d) ? "1" : "0");
        
        switch(pattern)
        {
            case "0001": //p14, p24, p34   
                AddTriangle(FindPoint(a,d), FindPoint(b,d), FindPoint(c,d)); 
                break;
            case "1110": //p14, p34, p24               
                AddTriangle(FindPoint(a,d), FindPoint(c,d), FindPoint(b,d));
                break;
            case "0010": //p13, p34, p23
                AddTriangle(FindPoint(a,c), FindPoint(c,d), FindPoint(b,c));
                break; 
            case "1101": //p13, p23, p34         
                AddTriangle(FindPoint(a,c), FindPoint(b,c), FindPoint(c,d));
                break;
            case "0100": //p12, p23, p24
                AddTriangle(FindPoint(a,b), FindPoint(b,c), FindPoint(b,d));
                break; 
            case "1011": // p12, p24, p23
                AddTriangle(FindPoint(a,b), FindPoint(b,d), FindPoint(b,c));
                break;
            case "1000": //p12, p14, p13
                AddTriangle(FindPoint(a,b), FindPoint(a,d), FindPoint(a,c));
                break;
            case "0111": //p12, p13, p14
                AddTriangle(FindPoint(a,b), FindPoint(a,c), FindPoint(a,d));
                break;
            case "0011": //p13, p14, p24, p23
                AddTriangle(FindPoint(a,c), FindPoint(a,d), FindPoint(b,d), FindPoint(b,c));
                break;
            case "1100": //p13, p23, p24, p14
                AddTriangle(FindPoint(a,c), FindPoint(b,c), FindPoint(b,d), FindPoint(a,d));
                break;
            case "1010": //p12, p14, p34, p23
                AddTriangle(FindPoint(a,b), FindPoint(a,d), FindPoint(c,d), FindPoint(b,c));
                break;
            case "0101": //p12, p23, p34, p14
                AddTriangle(FindPoint(a,b), FindPoint(b,c), FindPoint(c,d), FindPoint(a,d));
                break;
            case "0110": //p12, p13, p34, p24
                AddTriangle(FindPoint(a,b), FindPoint(a,c), FindPoint(c,d), FindPoint(b,d));
                break;
            case "1001": //p12, p24, p34, p13
                AddTriangle(FindPoint(a,b), FindPoint(b,d), FindPoint(c,d), FindPoint(a,c));
                break;
            default:
                break;
        }
    }

    Vector3 FindPoint(Vector3 a, Vector3 b)
    {
        float v1 = FindDistance(a), v2 = FindDistance(b), v;
        if (v1 < v2)
        {
            v = 1f - (_iso - v2) / (v1 - v2);
            return Vector3.Lerp(b,a,v); 
        } else 
        {
            v = 1f - (_iso - v1) / (v2 - v1);
            return Vector3.Lerp(a,b,v);
        }
    }

    bool MatchPattern(bool[] a, bool[] b)
    {
        return a.SequenceEqual(b) || a.SequenceEqual(b.Select(b => !b).ToArray());
    }
    
    bool IsInside(Vector3 v)
    {
        return FindDistance(v) < _iso * 2;       //TODO: Heller endre slideren enn * 2!
    }

    float FindDistance(Vector3 v)
    {
        Vector3 center = new(xdim/2,xdim/2,xdim/2);
        return Vector3.Distance(v, center); //Vector3.Distance(new Vector2(x,y), center) denne verdier skal brukes mot _iso
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        vertices.Add(Normalize(a));
        vertices.Add(Normalize(b));
        vertices.Add(Normalize(c));
        indices.Add(vertices.Count - 1);
        indices.Add(vertices.Count - 2);
        indices.Add(vertices.Count - 3);
    }

    void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        AddTriangle(a, b, c);
        AddTriangle(a, c, d);
    }

    Vector3 Normalize(Vector3 a)
    {
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

    ushort pixelval(Vector2 p, int xdim, ushort[] pixels)
    {
        return pixels[(int)p.x + (int)p.y * xdim];
    }

    private void OnToggleValueChanged(ChangeEvent<bool> evt)
    {
        checkedToggle = evt.newValue;
        print("toggle: " + checkedToggle);
    }
       
    public void slicePosSlider1Change(ChangeEvent<float> evt)
    {
        if (checkedToggle) 
        {
        _slider1.value = evt.newValue;
        _iso = (int) evt.newValue;
        DrawFilledCircle(red, green, blue);
        } else {
            //int n = _slices.Length;
            int n = (int) _slider2.value - 2; //for å teste (begynner på 3).
            int total = (int) (n * (_slider1.value + 0.9) / 100);
            print("n: " + n + ". value: " + _slider1.value + ". Total: " + total);
            //setTexture(_slices[(int) (_slider1.value * n) / 100]);
        } //finn elegant løsning for å få med første og siste, men begynner på 0....
    }    
    
    public void slicePosSlider2Change(ChangeEvent<float> evt)
    {
        _slider2.value = evt.newValue;
        divitions = (int) _slider2.value;
        //DrawCircleByPoints();
    }
    
    private void slicePosSliderRedChange(ChangeEvent<float> evt)
    {
        red = evt.newValue;
        DrawFilledCircle(red, green, blue);
    }

    
    private void slicePosSliderGreenChange(ChangeEvent<float> evt)
    {
        green = evt.newValue;
        DrawFilledCircle(red, green, blue);
    }

    
    private void slicePosSliderBlueChange(ChangeEvent<float> evt)
    {
        blue = evt.newValue;
        DrawFilledCircle(red, green, blue);
    }
   
    public void sliceIsoSliderChange(float val)
    {
        print("sliceIsoSliderChange:" + val); 
    }
    
    public void button1Pushed(ClickEvent evt)
    {
        DrawMesh(); 
    }

    public void button2Pushed(ClickEvent evt)
    {
        mscript.MeshToFile("test.obj");
        print("button2Pushed"); 
    }
}