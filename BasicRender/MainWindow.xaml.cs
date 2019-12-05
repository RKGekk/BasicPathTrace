using BasicRender.Engine;
using MathematicalEntities;
using PhysicsEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BasicRender {

    public struct OneLineCacheStruct {
        public long data1;
        public long data2;
        public long data3;
        public long data4;
        public long data5;
        public long data6;
        public long data7;
        public long data8;
    }

    public struct pLine {
        public pLine(int x0, int y0, int x1, int y1) {
            this.x0 = x0;
            this.y0 = y0;
            this.x1 = x1;
            this.y1 = y1;
        }
        public int x0;
        public int y0;
        public int x1;
        public int y1;
    }

    public struct pBGRA {
        public pBGRA(int blue, int green, int red, int alpha) {
            this.blue = blue;
            this.green = green;
            this.red = red;
            this.alpha = alpha;
        }
        public int blue;
        public int green;
        public int red;
        public int alpha;
    }

    public partial class MainWindow : Window {

        private const int MAX_RAY_DEPTH = 4;
        private const float INFINITY = 100000000.0f;
        private const float M_PI = 3.141592653589793f;

        private GameTimer _timer = new GameTimer();

        private WriteableBitmap _wbStat;
        private Int32Rect _rectStat;
        private byte[] _pixelsStat;
        private int _strideStat;
        private int _pixelWidthStat;
        private int _pixelHeightStat;

        private WriteableBitmap _wb;
        private Int32Rect _rect;
        private byte[] _pixels;
        private int _stride;
        private int _pixelWidth;
        private int _pixelHeight;

        public MainWindow() {
            InitializeComponent();
        }

        static void printLine(byte[] buf, pLine lineCoords, pBGRA color, int pixelWidth) {

            int stride = (pixelWidth * 32) / 8;
            int pixelHeight = buf.Length / stride;

            int x0 = lineCoords.x0;
            int y0 = lineCoords.y0;
            int x1 = lineCoords.x1;
            int y1 = lineCoords.y1;

            int dx = Math.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;

            int dy = Math.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;

            int err = (dx > dy ? dx : -dy) / 2;
            int e2;

            for (; ; ) {

                if (!(x0 >= pixelWidth || y0 >= pixelHeight || x0 < 0 || y0 < 0))
                    printPixel(buf, x0, y0, color, pixelWidth);

                if (x0 == x1 && y0 == y1)
                    break;

                e2 = err;

                if (e2 > -dx) {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dy) {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        static void printPixel(byte[] buf, int x, int y, pBGRA color, int pixelWidth) {

            int blue = color.blue;
            int green = color.green;
            int red = color.red;
            int alpha = color.alpha;

            int pixelOffset = (x + y * pixelWidth) * 32 / 8;
            buf[pixelOffset] = (byte)blue;
            buf[pixelOffset + 1] = (byte)green;
            buf[pixelOffset + 2] = (byte)red;
            buf[pixelOffset + 3] = (byte)alpha;
        }

        static void fillScreen(byte[] buf, pBGRA color, int pixelWidth) {

            int stride = (pixelWidth * 32) / 8;
            int pixelHeight = buf.Length / stride;

            for (int y = 0; y < pixelHeight; y++)
                for (int x = 0; x < pixelWidth; x++)
                    printPixel(buf, x, y, color, pixelWidth);
        }

        static void lmoveScreen(byte[] buf, pBGRA fillColor, int moveAmt, int pixelWidth) {

            int stride = (pixelWidth * 32) / 8;
            int pixelHeight = buf.Length / stride;

            for (int y = 0; y < pixelHeight; y++) {
                for (int x = 0; x < pixelWidth; x++) {

                    int nextPixel = x + moveAmt;
                    if (nextPixel < pixelWidth) {
                        int pixelOffset = (nextPixel + y * pixelWidth) * 32 / 8;
                        printPixel(buf, x, y, new pBGRA(buf[pixelOffset], buf[pixelOffset + 1], buf[pixelOffset + 2], buf[pixelOffset + 3]), pixelWidth);
                    }
                    else {
                        printPixel(buf, x, y, fillColor, pixelWidth);
                    }
                }
            }
        }

        public float mix(float a, float b, float mix) {
            return b * mix + a * (1 - mix);
        }

        public Vec3f trace(Vec3f rayorig, Vec3f raydir, List<Sphere> spheres, int depth) {

            float tnear = INFINITY;
            Sphere sphere = null;

            //find intersection of this ray with the sphere in the scene
            int spCount = spheres.Count();
            for (int i = 0; i < spCount; ++i) {

                float t0 = INFINITY;
                float t1 = INFINITY;
                (bool, float, float) inter = spheres[i].intersect(rayorig, raydir);
                if (inter.Item1) {
                    t0 = inter.Item2;
                    t1 = inter.Item3;
                    if (t0 < 0) t0 = t1;
                    if (t0 < tnear) {
                        tnear = t0;
                        sphere = spheres[i];
                    }
                }
            }

            //if there's no intersection return black or background color
            if (sphere == null) return new Vec3f(1.0f);

            Vec3f surfaceColor = new Vec3f(0.0f);   //color of the ray/surfaceof the object intersected by the ray
            Vec3f phit = rayorig + raydir * tnear;  //point of intersection
            Vec3f nhit = phit - sphere.center;      //normal at the intersection point
            nhit.normalize();                       //normalize normal direction

            //If the normal and the view direction are not opposite to each other
            //reverse the normal direction. That also means we are inside the sphere so set
            //the inside bool to true. Finally reverse the sign of IdotN which we want
            //positive.
            float bias = 0.0001f; //add some bias to the point from which we will be tracing
            bool inside = false;
            if (raydir.dot(nhit) > 0) {
                nhit = -1 * nhit;
                inside = true;
            }

            if ((sphere.transparency > 0 || sphere.reflection > 0) && depth < MAX_RAY_DEPTH) {

                float facingratio = -raydir.dot(nhit);

                // change the mix value to tweak the effect
                float fresneleffect = mix((float)Math.Pow(1 - facingratio, 3), 1, 0.1f);

                // compute reflection direction (not need to normalize because all vectors
                // are already normalized)
                Vec3f refldir = raydir.reflect(nhit);
                refldir.normalize();
                Vec3f reflection = trace(phit + nhit * bias, refldir, spheres, depth + 1);
                Vec3f refraction = new Vec3f(0.0f);

                // if the sphere is also transparent compute refraction ray (transmission)
                if (sphere.transparency != 0.0f) {

                    float ior = 1.1f;
                    float eta = (inside) ? ior : 1 / ior; // are we inside or outside the surface?
                    float cosi = -nhit.dot(raydir);
                    float k = 1 - eta * eta * (1 - cosi * cosi);
                    Vec3f refrdir = raydir * eta + nhit * (eta * cosi - (float)Math.Sqrt(k));
                    refrdir.normalize();
                    refraction = trace(phit - nhit * bias, refrdir, spheres, depth + 1);
                }

                // the result is a mix of reflection and refraction (if the sphere is transparent)
                surfaceColor = (reflection * fresneleffect + refraction * (1.0f - fresneleffect) * sphere.transparency) * sphere.surfaceColor;
            }
            else {

                // it's a diffuse object, no need to raytrace any further
                for (int i = 0; i < spCount; ++i) {
                    if (spheres[i].emissionColor.x > 0) {

                        // this is a light
                        Vec3f transmission = new Vec3f(1.0f);
                        Vec3f lightDirection = spheres[i].center - phit;
                        lightDirection.normalize();

                        for (int j = 0; j < spCount; ++j) {
                            if (i != j) {

                                if (spheres[j].intersect(phit + nhit * bias, lightDirection).Item1) {
                                    transmission = new Vec3f(0.0f);
                                    break;
                                }
                            }
                        }

                        surfaceColor += sphere.surfaceColor * transmission * Math.Max(0.0f, nhit.dot(lightDirection)) * spheres[i].emissionColor;
                    }
                }
            }

            return surfaceColor + sphere.emissionColor;
        }

        public class lineToRender {
            public int from;
            public int to;
        }

        public lineToRender[] lineTos;

        void render(byte[] buf, List<Sphere> spheres, int pixelWidth) {

            float tm = _timer.gameTime() * 2.0f;

            int stride = (pixelWidth * 32) / 8;
            int pixelHeight = buf.Length / stride;

            int width = pixelWidth;
            int height = pixelHeight;

            float invWidth = 1.0f / (float)width;
            float invHeight = 1.0f / (float)height;
            float fov = 40.0f;
            float aspectratio = width / (float)height;
            float angle = (float)Math.Tan(M_PI * 0.5f * fov / 180.0f);

            Parallel.For(0, lineTos.Length, i => {
                lineToRender toRender = lineTos[i];
                for (int y = toRender.from; y < toRender.to; ++y) {
                    for (int x = 0; x < width; ++x) {

                        float xx = (2.0f * (((float)x + 0.5f) * invWidth) - 1.0f) * angle * aspectratio;
                        float yy = (1.0f - 2.0f * (((float)y + 0.5f) * invHeight)) * angle;
                        Vec3f raydir = new Vec3f(xx, yy, -1.0f);
                        raydir.normalize();

                        Vec3f raydir2 = new Vec3f(Mat4f.RotationYMatrix(-tm) * raydir);
                        Vec3f rayorig = new Vec3f(0.0f, 5.0f, -25.0f) + new Vec3f(Mat4f.RotationYMatrix(-tm) * new Vec3f(0.0f, 5.0f, 25.0f));
                        Vec3f pixel = trace(rayorig, raydir2, spheres, 0);

                        printPixel(buf, x, y, new pBGRA((byte)(pixel.z * 255.0f), (byte)(pixel.y * 255.0f), (byte)(pixel.x * 255.0f), 255), pixelWidth);
                    }
                }
            });
        }

        List<Sphere> _spheres = new List<Sphere>();

        private void Window_Initialized(object sender, EventArgs e) {

            _timer.reset();
            _timer.start();

            _pixelWidth = (int)img.Width;
            _pixelHeight = (int)img.Height;

            int stride = (_pixelWidth * 32) / 8;

            lineTos = new lineToRender[] {
                new lineToRender() {from = 0, to = 18},
                new lineToRender() {from = 18, to = 36},
                new lineToRender() {from = 36, to = 54},
                new lineToRender() {from = 54, to = 72},
                new lineToRender() {from = 72, to = 90},
                new lineToRender() {from = 90, to = 108},
                new lineToRender() {from = 108, to = 126},
                new lineToRender() {from = 126, to = 144},
                new lineToRender() {from = 144, to = 162},
                new lineToRender() {from = 162, to = 180},
                new lineToRender() {from = 180, to = 198},
                new lineToRender() {from = 198, to = 216},
                new lineToRender() {from = 216, to = 234},
                new lineToRender() {from = 234, to = 252},
                new lineToRender() {from = 252, to = 270},
                new lineToRender() {from = 270, to = 288},
                new lineToRender() {from = 288, to = 306},
                new lineToRender() {from = 306, to = 324},
                new lineToRender() {from = 324, to = 342},
                new lineToRender() {from = 342, to = 360},
                new lineToRender() {from = 360, to = 378},
                new lineToRender() {from = 378, to = 396},
                new lineToRender() {from = 396, to = 414},
                new lineToRender() {from = 414, to = 432},
                new lineToRender() {from = 432, to = 450},
                new lineToRender() {from = 450, to = 468},
                new lineToRender() {from = 468, to = 480},
            };

            _wb = new WriteableBitmap(_pixelWidth, _pixelHeight, 96, 96, PixelFormats.Bgra32, null);
            _rect = new Int32Rect(0, 0, _pixelWidth, _pixelHeight);
            _pixels = new byte[_pixelWidth * _pixelHeight * _wb.Format.BitsPerPixel / 8];

            fillScreen(_pixels, new pBGRA(128, 128, 128, 255), _pixelWidth);
            printLine(_pixels, new pLine(0, 0, 64, 56), new pBGRA(195, 94, 65, 255), _pixelWidth);

            _stride = (_wb.PixelWidth * _wb.Format.BitsPerPixel) / 8;
            _wb.WritePixels(_rect, _pixels, _stride, 0);

            img.Source = _wb;

            // position, radius, surface color, reflectivity, transparency, emission color
            Sphere sphere1 = new Sphere(new Vec3f(0.0f, -10005, -20), 10000, new Vec3f(0.20f, 0.20f, 0.20f), 0, 0.0f);
            _spheres.Add(sphere1);

            Sphere sphere2 = new Sphere(new Vec3f(0.0f, 10, -20), 4.0f, new Vec3f(1.00f, 0.32f, 0.36f), 1, 0.9f);
            _spheres.Add(sphere2);

            Sphere sphere3 = new Sphere(new Vec3f(5.0f, 10, -15), 2, new Vec3f(0.90f, 0.76f, 0.46f), 1, 0.9f);
            _spheres.Add(sphere3);

            Sphere sphere4 = new Sphere(new Vec3f(5.0f, 10, -25), 3, new Vec3f(0.65f, 0.77f, 0.97f), 1, 0.9f);
            _spheres.Add(sphere4);

            // light
            _spheres.Add(new Sphere(new Vec3f(20.0f, 30, -40), 3, new Vec3f(0.00f, 0.00f, 0.00f), 0, 0.0f, new Vec3f(3.0f, 3.0f, 3.0f)));

            InitializeStats();

            CompositionTarget.Rendering += UpdateChildren;
        }

        private void InitializeStats() {

            _pixelWidthStat = (int)statImg.Width;
            _pixelHeightStat = (int)statImg.Height;

            _wbStat = new WriteableBitmap(_pixelWidthStat, _pixelHeightStat, 96, 96, PixelFormats.Bgra32, null);
            _rectStat = new Int32Rect(0, 0, _pixelWidthStat, _pixelHeightStat);
            _pixelsStat = new byte[_pixelWidthStat * _pixelHeightStat * _wbStat.Format.BitsPerPixel / 8];

            fillScreen(_pixelsStat, new pBGRA(32, 32, 32, 255), _pixelWidthStat);

            _strideStat = (_wbStat.PixelWidth * _wbStat.Format.BitsPerPixel) / 8;
            _wbStat.WritePixels(_rectStat, _pixelsStat, _strideStat, 0);

            statImg.Source = _wbStat;
        }

        private float _tt = 0.0f;

        protected void UpdateChildren(object sender, EventArgs e) {

            RenderingEventArgs renderingArgs = e as RenderingEventArgs;
            _timer.tick();

            float duration = _timer.deltaTime();

            _tt += duration;
            if (_tt > 1.0f)
                _tt = 0.0f;

            render(_pixels, _spheres, _pixelWidth);
            _wb.WritePixels(_rect, _pixels, _stride, 0);

            updateStats();
        }

        private void updateStats() {

            float duration = _timer.deltaTime();
            float totalTime = _timer.gameTime();
            int iduration = (int)(duration * 1000.0f);

            statsText.Text = $"RenderDuration: {duration * 1000.0f:F2}ms; FPS: {1.0f / duration:F0}; TotalTime: {totalTime:F3}sec";
        }
    }
}
