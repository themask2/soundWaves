using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace soundWaves
{

	public struct GraphicalWavePlot
	{
		private float minValue;
		private float maxValue;
		private float peakValue;
		public GraphicalWavePlot(float minValue, float maxValue, float peakValue)
		{
			this.minValue = minValue;
			this.maxValue = maxValue;
			this.peakValue = peakValue;
		}
		public bool CheckArea(int pos, int heightImg)
		{
			double Oh = heightImg / 2;
			double y0 = Oh - Math.Abs(minValue) * Oh / peakValue;
			double y1 = Oh + maxValue * Oh / peakValue;
			return (pos > y0 && pos < y1);
		}
	}


	public class PlottingGraphImg
	{
		private List<GraphicalWavePlot> waveSamples = new List<GraphicalWavePlot>();
		private SoftwareBitmap softwareBitmap;
		private WavFile wavFile;
		private Color backgroundColor = Color.FromArgb(0, 0, 0, 0);
		public Color BackgroundColor { get { return backgroundColor; } set { backgroundColor = value; } }
		private Color foregroundColor = Color.FromArgb(255, 255, 255, 255);
		public Color ForegroundColor { get { return foregroundColor; } set { foregroundColor = value; } }
		private int image_width;
		public int ImageWidth { get { return image_width; } set { image_width = value; } }
		private int image_height;
		public int ImageHeight { get { return image_height; } set { image_height = value; } }
		public PlottingGraphImg(WavFile _wavFile, int _image_width, int _image_height)
		{
			this.wavFile = _wavFile;
			this.image_width = _image_width; this.image_height = _image_height;
			BuildImage();
			CreateGraphicFile();
		}
		private void BuildImage()
		{
			int xPos = 2;
			int interval = 1;
			var yScale = ImageHeight;
			float[] readBuffer = wavFile.GetFloatBuffer();
			int samplesPerPixel = readBuffer.Length / ImageWidth;
			float negativeLimit = readBuffer.Take(readBuffer.Length).Min();
			float positiveLimit = readBuffer.Take(readBuffer.Length).Max();
			float peakValue = (positiveLimit > negativeLimit) ? (positiveLimit) : (negativeLimit);
			peakValue *= 1.2f;
			for (int i = 0; i < readBuffer.Length; i += samplesPerPixel, xPos += interval)
			{
				float[] partBuffer = new float[samplesPerPixel];
				int lengthPartBuffer = ((i + samplesPerPixel) > readBuffer.Length) ? (readBuffer.Length - i) : (samplesPerPixel);
				Array.Copy(readBuffer, i, partBuffer, 0, lengthPartBuffer);
				var min = partBuffer.Take(samplesPerPixel).Min();
				var max = partBuffer.Take(samplesPerPixel).Max();
				waveSamples.Add(new GraphicalWavePlot(minValue: min, maxValue: max, peakValue: peakValue));
			}
		}

		public unsafe void CreateGraphicFile()
		{
			softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, ImageWidth, ImageHeight);
			using (BitmapBuffer buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Write))
			{
				using (var reference = buffer.CreateReference())
				{
					byte* dataInBytes;
					uint capacity;
					((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacity); // Fill-in the BGRA plane
					BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
					for (int i = 0; i < bufferLayout.Width; i++)
					{
						for (int j = 0; j < bufferLayout.Height; j++)
						{
							Color tempColor = waveSamples[i].CheckArea(j, ImageHeight) ? ForegroundColor : BackgroundColor; //Blue 
							dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * j + 4 * i + 0] = (byte)tempColor.B; //Green 
							dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * j + 4 * i + 1] = (byte)tempColor.G; //Red 
							dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * j + 4 * i + 2] = (byte)tempColor.R; //Alpha 
							dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * j + 4 * i + 3] = (byte)tempColor.A;
						}
					}
				}
			}
		}
		public async Task SaveGraphicFile(StorageFile outputFile)
		{
			using (IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
			{
				BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
				encoder.SetSoftwareBitmap(softwareBitmap);
				encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
				encoder.IsThumbnailGenerated = true;
				try
				{
					await encoder.FlushAsync();
				}
				catch (Exception err)
				{
					switch (err.HResult)
					{
						case unchecked((int)0x88982F81):
							//WINCODEC_ERR_UNSUPPORTEDOPERATION 
							// If the encoder does not support writing a thumbnail, then try again 
							// but disable thumbnail generation. 
							encoder.IsThumbnailGenerated = false;
							break;
						default:
							throw err;
					}
				}
				if (encoder.IsThumbnailGenerated == false)
				{
					await encoder.FlushAsync();
				}
			}
		}
	}
}
