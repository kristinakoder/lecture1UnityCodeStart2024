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

    int _iso = 20, divitions = 10;

    List<Vector3> vertices = new(); //3. tall er 0 alltid.
    List<int> indices = new(); //legger til 0 og 1, da tegnes kant fra punkt 0 til 1. Legger til 1 og 2, kant fra 1 til 2 osv...

    private Button _button;
    private Toggle _toggle;
    private Slider _slider1, _slider2, _sliderRed, _sliderGreen, _sliderBlue;
    

    // Use this for initialization
    void Start () 
    {
        var uiDocument = GameObject.Find("MyUIDocument").GetComponent<UIDocument>();
        _button = uiDocument.rootVisualElement.Q("button1") as Button;
        _toggle = uiDocument.rootVisualElement.Q("toggle1") as Toggle;
        _slider1 = uiDocument.rootVisualElement.Q("slider1") as Slider;
        _slider2 = uiDocument.rootVisualElement.Q("slider2") as Slider;
        _sliderRed = uiDocument.rootVisualElement.Q("sliderR") as Slider;
        _sliderGreen = uiDocument.rootVisualElement.Q("sliderG") as Slider;
        _sliderBlue = uiDocument.rootVisualElement.Q("sliderB") as Slider;
        _button.RegisterCallback<ClickEvent>(button1Pushed);
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
        DrawFilledCircle(red, green, blue);                     // shows the first slice

        //  gets the mesh object and uses it to create a diagonal line
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        List<Vector3> vertices = new List<Vector3>();
        List<int> indices = new List<int>();
        vertices.Add(new Vector3(-0.5f,-0.5f,0));
        vertices.Add(new Vector3(0.5f,0.5f,0));
        indices.Add(0);
        indices.Add(1);
        mscript.createMeshGeometry(vertices, indices);
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
                if (IsInside(x, y)) texture.SetPixel(x, y, new UnityEngine.Color(r, g, b));
            }

        texture.filterMode = FilterMode.Point;  // nearest neigbor interpolation is used.  (alternative is FilterMode.Bilinear)
        texture.Apply();  // Apply all SetPixel calls
        GetComponent<Renderer>().material.mainTexture = texture;
    }

    void DrawCircleByPoints() 
    {
        vertices.Clear();
        indices.Clear();
        meshScript mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        step = (float) xdim/divitions;
        half = step/2;
        for (float x = half; x < xdim; x += step)        
            for (float y = half; y < ydim; y += step)
                //for (float z = half; z < ydim; z += step)
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

    bool MatchPattern(bool[] a, bool[] b)
    {
        return a.SequenceEqual(b) || a.SequenceEqual(b.Select(b => !b).ToArray());
    }
    
    bool IsInside(float x, float y)
    {
        return FindDistance(x,y) < _iso * 2;       
    }

    float FindDistance(float x, float y)
    {
        Vector2 center = new(xdim/2, ydim/2);
        return Vector3.Distance(new Vector2(x,y), center); //Vector3.Distance(new Vector2(x,y), center) denne verdier skal brukes mot _iso
    }
    
    bool[] CheckSquares(float x, float y)
    {
        return new bool[] { IsInside(x, y), IsInside(x + step, y), IsInside(x + step, y + step), IsInside(x, y + step) };
    }

    void AddVertexAndIndices(float a, float b, float c, float d)
    {
        Func<float, float> normalize = (x) => x / xdim - 0.5f;
        vertices.Add(new Vector3(normalize(a),normalize(b)));
        vertices.Add(new Vector3(normalize(c),normalize(d)));
        indices.Add(vertices.Count - 2);
        indices.Add(vertices.Count - 1);
    }    

    ushort pixelval(Vector2 p, int xdim, ushort[] pixels)
    {
        return pixels[(int)p.x + (int)p.y * xdim];
    }

    Vector2 vec2(float x, float y)
    {
        return new Vector2(x, y);
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
        DrawCircleByPoints();
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
        DrawCircleByPoints();
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
        
        print("button1Pushed"); 
        
    }

    public void button2Pushed()
    {
          print("button2Pushed"); 
    }

}