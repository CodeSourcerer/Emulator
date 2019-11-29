using System;
using System.Threading;
using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;

namespace AudioTest
{
    class AudioTest
    {
        AudioContext context;

        public static void Main(string[] args)
        {
            AudioTest test = new AudioTest();
            test.Run();
        }

        public void Run()
        {
            using (context = new AudioContext())
            {
                Console.WriteLine("Version: {0}", AL.Get(ALGetString.Version));
                Console.WriteLine("Vendor: {0}", AL.Get(ALGetString.Vendor));
                Console.WriteLine("Renderer: {0}", AL.Get(ALGetString.Renderer));

                int[] tribuffers, sinbuffers, sources;
                sinbuffers = AL.GenBuffers(2);
                tribuffers = AL.GenBuffers(2);
                sources = AL.GenSources(1);

                int sampleFreq = 44100;
                //var sinData1 = generateSinWave(100, sampleFreq, 1000);
                //var sinData2 = generateSinWave(1500, sampleFreq, 500);
                //var triData1 = generateTriWave(440, sampleFreq, 500);
                //var triData2 = generateTriWave(100, sampleFreq, 500);
                var sqData1 = generateSquareWave(940, sampleFreq, 2000);

                AL.BufferData(tribuffers[0], ALFormat.Mono16, sqData1, sqData1.Length, sampleFreq);
                //AL.BufferData(tribuffers[1], ALFormat.Mono16, triData2, triData2.Length, sampleFreq);

                //AL.Source(trisource, ALSourcei.Buffer, tribuffer);
                //AL.Source(trisource, ALSourceb.Looping, true);
                //AL.SourcePlay(trisource);

                //Console.WriteLine("Triangle Waves");

                int oscillations = 0;
                int sourceNum = oscillations % 2;
                AL.SourceQueueBuffer(sources[0], tribuffers[sourceNum]);
                AL.SourcePlay(sources[sourceNum]);
                //do
                //{
                //    oscillations++;
                //    sourceNum = oscillations % 2;
                //    AL.SourceQueueBuffer(sources[0], tribuffers[sourceNum]);
                //} while (oscillations <= 5);

                //AL.SourceStop(trisource);

                Console.WriteLine("Press a key to play sine wave");
                Console.ReadKey();
                //AL.SourceUnqueueBuffer(sources[0]);

                //AL.BufferData(sinbuffers[0], ALFormat.Mono16, sinData1, sinData1.Length, sampleFreq);
                ////AL.Source(sinsources[0], ALSourcei.Buffer, sinbuffers[0]);
                //AL.BufferData(sinbuffers[1], ALFormat.Mono16, sinData2, sinData2.Length, sampleFreq);
                ////AL.Source(sinsources[1], ALSourcei.Buffer, sinbuffers[1]);
                ////AL.Source(sinsources[1], ALSourceb.Looping, true);


                //oscillations = 0;
                //sourceNum = oscillations % 2;
                //AL.SourceQueueBuffer(sources[0], sinbuffers[sourceNum]);
                //AL.SourcePlay(sources[sourceNum]);

                //do
                //{
                //    oscillations++;
                //    sourceNum = oscillations % 2;
                //    AL.SourceQueueBuffer(sources[0], sinbuffers[sourceNum]);
                //} while (oscillations <= 5);

                //Console.WriteLine("Sine Wave - Press a key to stop");
                //Console.ReadKey();
            }
        }

        private short[] generateSinWave(int freq, int sampleRate, int sampleLengthMS)
        {
            double dt = 2 * Math.PI / sampleRate;
            double amp = 0.5 * short.MaxValue;
            var dataCount = sampleRate * (sampleLengthMS / 1000.0f);
            var sinData = new short[(int)dataCount];

            for (int i = 0; i < sinData.Length; i++)
            {
                sinData[i] = (short)(amp * Math.Sin(i * dt * freq));
            }

            return sinData;
        }

        private short[] generateTriWave(int freq, int sampleRate, int sampleLengthMS)
        {
            short period = (short)((sampleRate / freq) - 1);
            var dataCount = (int)(sampleRate * (sampleLengthMS / 1000.0f));
            var triData = new short[dataCount];
            double amp = short.MaxValue * 0.5;

            for (int i = 0; i < triData.Length; i++)
            {
                triData[i] = (short)((1 - 2 * Math.Abs(Math.Round((double)((1/period) * i), MidpointRounding.AwayFromZero) - ((1.0d/period) * i))) * amp);
            }

            return triData;
        }

        private short[] generateSquareWave(int freq, int sampleRate, int sampleLengthMS)
        {
            short period = (short)((sampleRate / freq) - 1);
            int dataCount = (int)(sampleRate * (sampleLengthMS / 1000.0f));
            double dt = 2 * Math.PI / sampleRate;
            var data = new short[dataCount];
            double amp = short.MaxValue * 0.5;

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (short)(amp * Math.Sign(Math.Sin(i * dt * freq)));
            }

            return data;
        }
    }
}
