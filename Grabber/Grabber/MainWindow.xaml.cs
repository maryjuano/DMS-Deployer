using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
using VideoFrameAnalyzer;

namespace Grabber
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private readonly FrameGrabber<LiveCameraResult> _grabber = null;
        private DateTime _startTime;
        private static readonly ImageEncodingParam[] s_jpegParams = {
            new ImageEncodingParam(ImwriteFlags.JpegQuality, 60)
        };
        private readonly IFaceServiceClient _faceClient =
           new FaceServiceClient("6ffc633acd0b4fbfa417985ec19dab12", "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");
        private LiveCameraResult _latestResultsToDisplay = null;
        private int customerCounter = 0;
        Guid _unknownPersonId;
        List<Guid> _employeeCondidates = new List<Guid>();
        Task<Person[]> employees;
        public MainWindow()
        {
            InitializeComponent();
            // initialize employees
            InitializeLocalVariables();



            // Create grabber. 
            _grabber = new FrameGrabber<LiveCameraResult>();

            // Set up a listener for when the client receives a new frame.
            _grabber.NewFrameProvided += (s, e) =>
            {
                //The callback may occur on a different thread, so we must use the
                // MainWindow.Dispatcher when manipulating the UI.
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    // Display the image in the pane
                    ImageSource.Source = VisualizeResult(e.Frame);
                }));

            };


            // Set up a listener for when the client receives a new result from an API call. 
            _grabber.NewResultAvailable += (s, e) =>
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (e.TimedOut)
                    {
                        MessageArea.Text = "API call timed out.";
                    }
                    else if (e.Exception != null)
                    {
                        string apiName = "";
                        string message = e.Exception.Message;
                        var faceEx = e.Exception as FaceAPIException;
                        var emotionEx = e.Exception as Microsoft.ProjectOxford.Common.ClientException;
                        if (faceEx != null)
                        {
                            apiName = "Face";
                            message = faceEx.ErrorMessage;
                        }
                        MessageArea.Text = string.Format("{0} API call failed on frame {1}. Exception: {2}", apiName, e.Frame.Metadata.Index, message);
                    }
                    else
                    {
                        _latestResultsToDisplay = e.Analysis;

                        ImageSource.Source = VisualizeResult(e.Frame);

                        DisplayResult();

                        SortCandidates(_latestResultsToDisplay.Faces, e.Frame.Image.ToMemoryStream(".jpg", s_jpegParams));
                    }
                }));
            };


            _grabber.AnalysisFunction = FacesAnalysisFunction;
        }


        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            await _grabber.StopProcessingAsync();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CameraList.HasItems)
            {
                MessageArea.Text = "No cameras found; cannot start processing";
                return;
            }

            //// Clean leading/trailing spaces in API keys. 
            //Properties.Settings.Default.FaceAPIKey = Properties.Settings.Default.FaceAPIKey.Trim();
            //Properties.Settings.Default.EmotionAPIKey = Properties.Settings.Default.EmotionAPIKey.Trim();
            //Properties.Settings.Default.VisionAPIKey = Properties.Settings.Default.VisionAPIKey.Trim();

            //// Create API clients. 
            //_faceClient = new FaceServiceClient(Properties.Settings.Default.FaceAPIKey, Properties.Settings.Default.FaceAPIHost);
            //_emotionClient = new EmotionServiceClient(Properties.Settings.Default.EmotionAPIKey, Properties.Settings.Default.EmotionAPIHost);
            //_visionClient = new VisionServiceClient(Properties.Settings.Default.VisionAPIKey, Properties.Settings.Default.VisionAPIHost);

            // How often to analyze. 
            _grabber.TriggerAnalysisOnInterval(Settings.Default.AnalysisInterval);

            // Reset message. 
            MessageArea.Text = "";

            // Record start time, for auto-stop
            _startTime = DateTime.Now;

            await _grabber.StartProcessingCameraAsync(CameraList.SelectedIndex);
        }

        /// <summary> Populate CameraList in the UI, once it is loaded. </summary>
        /// <param name="sender"> Source of the event. </param>
        /// <param name="e">      Routed event information. </param>
        private void CameraList_Loaded(object sender, RoutedEventArgs e)
        {
            int numCameras = _grabber.GetNumCameras();

            if (numCameras == 0)
            {
                MessageArea.Text = "No cameras found!";
            }

            var comboBox = sender as ComboBox;
            comboBox.ItemsSource = Enumerable.Range(0, numCameras).Select(i => string.Format("Camera {0}", i + 1));
            comboBox.SelectedIndex = 0;
        }

        /// <summary> Function which submits a frame to the Face API. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the faces returned by the API. </returns>
        private async Task<LiveCameraResult> FacesAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);

            // Submit image to API. 
            var attrs = new List<FaceAttributeType> { FaceAttributeType.Age,
                FaceAttributeType.Gender, FaceAttributeType.HeadPose, FaceAttributeType.Emotion };
            var faces = await _faceClient.DetectAsync(jpg, returnFaceAttributes: attrs);

            // Output. 
            return new LiveCameraResult { Faces = faces };
        }

        private async void InitializeLocalVariables()
        {
          //  employees = _faceClient.ListPersonsAsync("employeeid");
            var unknownPersons = await _faceClient.ListPersonsAsync("unknownid");
            var unknown = unknownPersons.SingleOrDefault(p => p.Name == "Unknown");

            if (unknown != null)
            {
                _unknownPersonId = unknown.PersonId;
            }
        }

        private async void SortCandidates(Face[] faces, Stream imageStream)
        {
            if (faces.Length == 0) return;

            var faceIds = faces.Select(face => face.FaceId).ToArray();
            var employeeMatches = await _faceClient.IdentifyAsync("employeeid", faceIds);
            var unknownMatches = await _faceClient.IdentifyAsync("unknownid", faceIds);

            IEnumerable<IdentifyResult> employeeIdentified = employeeMatches
                .Where(e => e.Candidates.Count() > 0 && e.Candidates[0]?.Confidence > 0.6);           

            IEnumerable<IdentifyResult> unknownIdentified = unknownMatches
                .Where(u => u.Candidates.Count() > 0 && u.Candidates[0]?.Confidence > 0.6);
             

            _employeeCondidates.AddRange(employeeIdentified.Select(p => p.Candidates.ElementAt(0)).Select(p => p.PersonId));

            txtRexValue.Text = _employeeCondidates.Distinct().Count().ToString();

            IEnumerable<Guid> employeeFaceIds = employeeIdentified.Select(p => p.FaceId);
            IEnumerable<Guid> unknownFaceIds = unknownIdentified.Select(u => u.FaceId);

            customerCounter += faces.Length - (employeeFaceIds.Count() + unknownFaceIds.Count());
            txtUnKnown.Text = customerCounter.ToString();

            var faceToBeAdded = faceIds.Where(p => !employeeFaceIds.Contains(p) && !unknownFaceIds.Contains(p));           

            AddUnknownFaces(imageStream, faceToBeAdded, faces);
        }

        private async void AddUnknownFaces(Stream imageStream, IEnumerable<Guid> faceIds, Face[] faces)
        {
            if (faceIds.Count() == 0) return;

            await IsTrainingComplete();

            foreach (Guid faceId in faceIds)
            {
                // unknown faces
                _faceClient.AddPersonFaceAsync(
                    "unknownid", _unknownPersonId, imageStream, null, faces.Single(f => f.FaceId == faceId).FaceRectangle);
            }

            _faceClient.TrainPersonGroupAsync("unknownid");
        }

        private async Task<bool> IsTrainingComplete()
        {
            TrainingStatus trainingStatus = null;
            while (true)
            {
                trainingStatus = await _faceClient.GetPersonGroupTrainingStatusAsync("unknownid");

                if (trainingStatus.Status == Status.Succeeded)
                {
                    return true;
                }

                await Task.Delay(250);
            }
        }


        private BitmapSource VisualizeResult(VideoFrame frame)
        {
            // Draw any results on top of the image. 
            BitmapSource visImage = frame.Image.ToBitmapSource();

            var result = _latestResultsToDisplay;

            if (result != null)
            {
                // See if we have local face detections for this image.
                var clientFaces = (OpenCvSharp.Rect[])frame.UserData;
                if (clientFaces != null && result.Faces != null)
                {
                    // If so, then the analysis results might be from an older frame. We need to match
                    // the client-side face detections (computed on this frame) with the analysis
                    // results (computed on the older frame) that we want to display. 
                    MatchAndReplaceFaceRectangles(result.Faces, clientFaces);
                }

                visImage = Visualization.DrawFaces(visImage, result.Faces, result.EmotionScores, result.IdentifiedFaces, null);
            }

            return visImage;
        }

        private void DisplayResult()
        {
            int faces = _latestResultsToDisplay.Faces.Count();
            txtNumberFaces.Text = faces.ToString();

            //int identified = 0;

            //foreach (KeyValuePair<string, string> item in _latestResultsToDisplay.IdentifiedFaces)
            //{
            //    if (item.Value == "Rex") identified++;
            //}

            //txtRexValue.Text = identified.ToString();

            //customerCounter += (faces - identified);

            //txtUnKnown.Text = customerCounter.ToString();
        }

        private Face CreateFace(Microsoft.ProjectOxford.Common.Rectangle rect)
        {
            return new Face
            {
                FaceRectangle = new FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        private void MatchAndReplaceFaceRectangles(Face[] faces, OpenCvSharp.Rect[] clientRects)
        {
            // Use a simple heuristic for matching the client-side faces to the faces in the
            // results. Just sort both lists left-to-right, and assume a 1:1 correspondence. 

            // Sort the faces left-to-right. 
            var sortedResultFaces = faces
                .OrderBy(f => f.FaceRectangle.Left + 0.5 * f.FaceRectangle.Width)
                .ToArray();

            // Sort the clientRects left-to-right.
            var sortedClientRects = clientRects
                .OrderBy(r => r.Left + 0.5 * r.Width)
                .ToArray();

            // Assume that the sorted lists now corrrespond directly. We can simply update the
            // FaceRectangles in sortedResultFaces, because they refer to the same underlying
            // objects as the input "faces" array. 
            for (int i = 0; i < Math.Min(faces.Length, clientRects.Length); i++)
            {
                // convert from OpenCvSharp rectangles
                OpenCvSharp.Rect r = sortedClientRects[i];
                sortedResultFaces[i].FaceRectangle = new FaceRectangle { Left = r.Left, Top = r.Top, Width = r.Width, Height = r.Height };
            }
        }

    }
}
