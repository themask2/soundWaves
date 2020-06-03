using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// O modelo de item de Página em Branco está documentado em https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x416

namespace soundWaves
{

    /// <summary>
    /// Uma página vazia que pode ser usada isoladamente ou navegada dentro de um Quadro.
    /// </summary>
    /// 
    public sealed partial class MainPage : Page
    {

        AudioFileInputNode _fileInputNode;
        AudioGraph _graph;
        AudioDeviceOutputNode _deviceOutputNode;

        private StorageFile currentFile;
        private PlottingGraphImg imgFile;

        public MainPage()
        {
            this.InitializeComponent();
        }

        //private async void NewFileHandler(object sender, RoutedEventArgs e)
        //{
        //    var picker = new Windows.Storage.Pickers.FileOpenPicker();
        //    picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
        //    picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;
        //    picker.FileTypeFilter.Add(".wma");
        //    picker.FileTypeFilter.Add(".mp3");

        //    Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
        //    if (file != null)
        //    {
        //        // Application now has read/write access to the picked file
        //        this.textBlock.Text = "Audio Selecionado: " + file.Name;
        //    }
        //    else
        //    {
        //        this.textBlock.Text = "Operação Cancelada.";
        //    }
        //}

        private async void ChooseFile_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.MusicLibrary;
            picker.FileTypeFilter.Add(".mp4");
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".wav");
            StorageFile file = await picker.PickSingleFileAsync();
            await ConvertToWaveFile(file);
        }

        public async Task ConvertToWaveFile(StorageFile sourceFile)
        {
            MediaTranscoder transcoder = new MediaTranscoder();
            MediaEncodingProfile profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Medium);
            CancellationTokenSource cts = new CancellationTokenSource(); //Create temporary file in temporary folder
            string fileName = String.Format("TempFile_{0}.wav", Guid.NewGuid());
            StorageFile temporaryFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(fileName);
            currentFile = temporaryFile;
            if (sourceFile == null || temporaryFile == null)
            {
                return;
            }
            try
            {
                var preparedTranscodeResult = await transcoder.PrepareFileTranscodeAsync(sourceFile, temporaryFile, profile);
                if (preparedTranscodeResult.CanTranscode)
                {
                    var progress = new Progress<double>((percent) => { Debug.WriteLine("Converting file: " + percent + "%"); });
                    await preparedTranscodeResult.TranscodeAsync().AsTask(cts.Token, progress);
                }
                else
                {
                    Debug.WriteLine("Error: Convert fail");
                }
            }
            catch
            {
                Debug.WriteLine("Error: Exception in ConvertToWaveFile");
            }
        }

        private async void BuildAndSaveImageFile_Click(object sender, RoutedEventArgs e)
        {
            WavFile wavFile = new WavFile(currentFile.Path.ToString());
            imgFile = new PlottingGraphImg(wavFile, 1000, 100);
            FileSavePicker fileSavePicker = new FileSavePicker();
            fileSavePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            fileSavePicker.FileTypeChoices.Add("JPEG files", new List<string>() { ".jpg" });
            fileSavePicker.SuggestedFileName = "image";
            var outputFile = await fileSavePicker.PickSaveFileAsync();
            if (outputFile == null)
            { // The user cancelled the picking operation 
                return;
            }
            await imgFile.SaveGraphicFile(outputFile);
        }

        private async void Play_OnClick(object sender, RoutedEventArgs e)
        {
            await CreateGraph();
            await CreateDefaultDeviceOutputNode();
            await CreateFileInputNode();

            AddReverb();

            ConnectNodes();

            _graph.Start();
        }

        /// <summary>
        /// Create an audio graph that can contain nodes
        /// </summary>       
        private async Task CreateGraph()
        {
            // Specify settings for graph, the AudioRenderCategory helps to optimize audio processing
            AudioGraphSettings settings = new AudioGraphSettings(Windows.Media.Render.AudioRenderCategory.Media);

            CreateAudioGraphResult result = await AudioGraph.CreateAsync(settings);

            if (result.Status != AudioGraphCreationStatus.Success)
            {
                throw new Exception(result.Status.ToString());
            }

            _graph = result.Graph;
        }

        /// <summary>
        /// Create a node to output audio data to the default audio device (e.g. soundcard)
        /// </summary>
        private async Task CreateDefaultDeviceOutputNode()
        {
            CreateAudioDeviceOutputNodeResult result = await _graph.CreateDeviceOutputNodeAsync();

            if (result.Status != AudioDeviceNodeCreationStatus.Success)
            {
                throw new Exception(result.Status.ToString());
            }

            _deviceOutputNode = result.DeviceOutputNode;
        }

        /// <summary>
        /// Ask user to pick a file and use the chosen file to create an AudioFileInputNode
        /// </summary>
        private async Task CreateFileInputNode()
        {
            FileOpenPicker filePicker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.MusicLibrary,
                FileTypeFilter = { ".mp3", ".wav" }
            };

            StorageFile file = await filePicker.PickSingleFileAsync();

            // file null check code omitted

            CreateAudioFileInputNodeResult result = await _graph.CreateFileInputNodeAsync(file);

            if (result.Status != AudioFileNodeCreationStatus.Success)
            {
                throw new Exception(result.Status.ToString());
            }

            _fileInputNode = result.FileInputNode;
        }

        /// <summary>
        /// Create an instance of the pre-supplied reverb effect and add it to the output node
        /// </summary>
        private void AddReverb()
        {
            ReverbEffectDefinition reverbEffect = new ReverbEffectDefinition(_graph)
            {
                DecayTime = 1
            };

            _deviceOutputNode.EffectDefinitions.Add(reverbEffect);
        }

        /// <summary>
        /// Connect all the nodes together to form the graph, in this case we only have 2 nodes
        /// </summary>
        private void ConnectNodes()
        {
            _fileInputNode.AddOutgoingConnection(_deviceOutputNode);
        }
    }
}
