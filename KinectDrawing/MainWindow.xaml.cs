using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using Microsoft.Kinect;
using Microsoft.Ink;
using KinectMouseController;

namespace KinectDrawing
{

    public partial class MainWindow : Window
    {
        private KinectSensor _sensor = null;
        private ColorFrameReader _colorReader = null;
        private BodyFrameReader _bodyReader = null;
        private IList<Body> _bodies = null;
        private int _width = 0;
        private int _height = 0;
        private byte[] _pixels = null;
        private WriteableBitmap _bitmap = null;

        public MainWindow()
        {
            InitializeComponent();

            _sensor = KinectSensor.GetDefault();

            if (_sensor != null)
            {
                _sensor.Open();


                _width = _sensor.ColorFrameSource.FrameDescription.Width;
                _height = _sensor.ColorFrameSource.FrameDescription.Height;

                _colorReader = _sensor.ColorFrameSource.OpenReader();
                _colorReader.FrameArrived += ColorReader_FrameArrived;

                _bodyReader = _sensor.BodyFrameSource.OpenReader();
                _bodyReader.FrameArrived += BodyReader_FrameArrived;
                _bodies = new Body[_sensor.BodyFrameSource.BodyCount];


                _pixels = new byte[_width * _height * 4];
                _bitmap = new WriteableBitmap(_width, _height, 96.0, 96.0, PixelFormats.Bgra32, null);

                _bodies = new Body[_sensor.BodyFrameSource.BodyCount];

                camera.Source = _bitmap;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_colorReader != null)
            {
                _colorReader.Dispose();
            }

            if (_bodyReader != null)
            {
                _bodyReader.Dispose();
            }

            if (_sensor != null)
            {
                _sensor.Close();
            }
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.CopyConvertedFrameDataToArray(_pixels, ColorImageFormat.Rgba);

                    _bitmap.Lock();
                    Marshal.Copy(_pixels, 0, _bitmap.BackBuffer, _pixels.Length);
                    _bitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
                    _bitmap.Unlock();
                }
            }
        }
        private void CreateSaveBitmap(Canvas canvas, string filename)
        {
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap((int)canvas.Width, (int)canvas.Height, 96d, 96d, PixelFormats.Pbgra32);
            canvas.Measure(new Size((int)canvas.Width, (int)canvas.Height));
            canvas.Arrange(new Rect(new Size((int)canvas.Width, (int)canvas.Height)));

            renderBitmap.Render(canvas);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            using (FileStream file = File.Create(filename))
            {
                encoder.Save(file);
            }
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.GetAndRefreshBodyData(_bodies);

                    Body body = _bodies.Where(b => b.IsTracked).FirstOrDefault();

                    if (body != null)
                    {
                        Joint handRight = body.Joints[JointType.HandRight];

                        if (handRight.TrackingState != TrackingState.NotTracked)
                        {
                            CameraSpacePoint handRightPosition = handRight.Position;
                            ColorSpacePoint handRightPoint = _sensor.CoordinateMapper.MapCameraPointToColorSpace(handRightPosition);

                            float x = handRightPoint.X;
                            float y = handRightPoint.Y;

                            if (!float.IsInfinity(x) && !float.IsInfinity(y))
                            {
                             
                                if (body.HandRightState == HandState.Lasso)
                                {

                                    KinectMouseMethods.SendMouseInput((int)x, (int)y, (int)trail.Width, (int)trail.Height, true);
                                }
                                if (body.HandRightState == HandState.Open)
                                {
                                    KinectMouseMethods.SendMouseInput((int)x, (int)y, (int)trail.Width, (int)trail.Height, false);
                                }
                                if (body.HandRightState == HandState.Closed)
                                {
                                    KinectMouseMethods.SendMouseInput((int)x, (int)y, (int)trail.Width, (int)trail.Height, false);
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        trail.Strokes.Save(ms);
                                        var myInkCollector = new InkCollector();
                                        var ink = new Ink();
                                        ink.Load(ms.ToArray());

                                        using (RecognizerContext context = new RecognizerContext())
                                        {
                                            if (ink.Strokes.Count > 0)
                                            {
                                                context.Strokes = ink.Strokes;
                                                RecognitionStatus status;
                                                var result = context.Recognize(out status);
                                                if (status == RecognitionStatus.NoError)
                                                {
                                                    if (result.TopString == "&" || result.TopString == "&")
                                                    {
                                                          textBox.Text += " ";
                                                    }
                                                    else if (result.TopString == "|" && textBox.Text != "")
                                                    {
                                                        textBox.Text = textBox.Text.Remove(textBox.Text.Length - 1);
                                                    }
                                                    else
                                                    {
                                                        textBox.Text += result.TopString;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    trail.Strokes.Clear();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
