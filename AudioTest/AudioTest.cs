using System;

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
            initialize();

            Console.WriteLine("Version: {0}", AL.Get(ALGetString.Version));
            Console.WriteLine("Vendor: {0}", AL.Get(ALGetString.Vendor));
            Console.WriteLine("Renderer: {0}", AL.Get(ALGetString.Renderer));

            int sinbuffer, tribuffer, sinsource, trisource;
            AL.GenBuffers(1, out sinbuffer);
            AL.GenBuffers(1, out tribuffer);
            AL.GenSources(1, out sinsource);
            AL.GenSources(1, out trisource);

            int sampleFreq = 44100;
            var sinData = generateSinWave(240, sampleFreq);
            var triData = generateTriWave(440, sampleFreq);

            AL.BufferData(tribuffer, ALFormat.Mono16, triData, triData.Length, sampleFreq);
            AL.Source(trisource, ALSourcei.Buffer, tribuffer);
            AL.Source(trisource, ALSourceb.Looping, true);

            AL.SourcePlay(trisource);

            Console.WriteLine("Triangle Wave - Press a key to stop");
            Console.ReadKey();
            //AL.SourceStop(source);

            AL.BufferData(sinbuffer, ALFormat.Mono16, sinData, sinData.Length, sampleFreq);
            AL.Source(sinsource, ALSourcei.Buffer, sinbuffer);
            AL.Source(sinsource, ALSourceb.Looping, true);

            AL.SourcePlay(sinsource);

            Console.WriteLine("Sine Wave - Press a key to stop");
            Console.ReadKey();

            cleanup();
        }

        private void initialize()
        {
            context = new AudioContext();
        }

        private void cleanup()
        {
            context.Dispose();
        }

        private short[] generateSinWave(int freq, int sampleFreq)
        {
            double dt = 2 * Math.PI / sampleFreq;
            double amp = 0.5;
            var dataCount = sampleFreq / freq;
            var sinData = new short[dataCount];

            for (int i = 0; i < sinData.Length; i++)
            {
                sinData[i] = (short)(amp * short.MaxValue * Math.Sin(i * dt * freq));
            }

            return sinData;
        }

        private short[] generateTriWave(int freq, int sampleFreq)
        {
            short period = (short)((sampleFreq / freq) - 1);
            var dataCount = (int)(sampleFreq / freq);
            var triData = new short[dataCount];
            double amp = short.MaxValue * 0.5;

            int slope = 1;
            for (int i = 0; i < triData.Length; i++)
            {
                triData[i] = (short)((1 - 2 * Math.Abs(Math.Round((double)((1/period) * i), MidpointRounding.AwayFromZero) - ((1.0d/period) * i))) * amp);
            }

            return triData;
        }
    }
}
