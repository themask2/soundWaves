using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace soundWaves
{
	public class WavFile
	{
		public string PathAudioFile { get; }
		private const int ticksInSecond = 10000000;
		private TimeSpan duration;
		public TimeSpan Duration { get { return duration; } }
		#region AudioData 
		private List<float> floatAudioBuffer = new List<float>();
		#endregion
		public WavFile(string _path)
		{
			PathAudioFile = _path;
			ReadWavFile(_path);
		}
		public float[] GetFloatBuffer()
		{
			return floatAudioBuffer.ToArray();
		}
		private void ReadWavFile(string filename)
		{
			try
			{
				using (FileStream fileStream = File.Open(filename, FileMode.Open))
				{
					BinaryReader reader = new BinaryReader(fileStream); // RIFF
					int chunkID = reader.ReadInt32();
					int fileSize = reader.ReadInt32();
					int riffType = reader.ReadInt32(); // Format 
					int fmtID;
					long _position = reader.BaseStream.Position;
					while (_position != reader.BaseStream.Length - 1)
					{
						reader.BaseStream.Position = _position;
						int _fmtId = reader.ReadInt32();
						if (_fmtId == 544501094)
						{
							fmtID = _fmtId;
							break;
						}
						_position++;
					}
					int fmtSize = reader.ReadInt32();
					int fmtCode = reader.ReadInt16();
					int channels = reader.ReadInt16();
					int sampleRate = reader.ReadInt32();
					int byteRate = reader.ReadInt32();
					int fmtBlockAlign = reader.ReadInt16();
					int bitDepth = reader.ReadInt16();
					if (fmtSize == 18)
					{
						int fmtExtraSize = reader.ReadInt16();
						reader.ReadBytes(fmtExtraSize);
					}
					int dataID = reader.ReadInt32();
					int dataSize = reader.ReadInt32();
					byte[] byteArray = reader.ReadBytes(dataSize);
					int bytesInSample = bitDepth / 8;
					int sampleAmount = dataSize / bytesInSample;
					float[] tempArray = null;
					switch (bitDepth)
					{
						case 16:
							Int16[] int16Array = new Int16[sampleAmount];
							System.Buffer.BlockCopy(byteArray, 0, int16Array, 0, dataSize);
							IEnumerable<float> tempInt16 = from i in int16Array select i / (float)Int16.MaxValue;
							tempArray = tempInt16.ToArray();
							break;
						default:
							return;
					}
					floatAudioBuffer.AddRange(tempArray);
					duration = DeterminateDurationTrack(channels, sampleRate);
				}
			}
			catch
			{
				Debug.WriteLine("File error");
				return;
			}
		}

		private TimeSpan DeterminateDurationTrack(int channels, int sampleRate)
		{
			long _duration = (long)(((double)floatAudioBuffer.Count / sampleRate / channels) * ticksInSecond);
			return TimeSpan.FromTicks(_duration);
		}
	}
}
