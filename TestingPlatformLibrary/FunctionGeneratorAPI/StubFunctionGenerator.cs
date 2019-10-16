using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Linq;

namespace TestingPlatformLibrary.FunctionGeneratorAPI
{
    /* The intended purpose of this class is for debugging application code.
     * 
     * This class represents a virtualized function generator, a stub. Commands recieved will be written out to a log file.
     * The purpose of this class is to provide a way to test implementations of the function generator interface without requiring
     * an actual function generator to be present for development. Attempting to use an actual function generator's implementation
     * without having it connected will result in errors. This class will not produce those errors.
     * To reduce clutter and storage usage
     * the log of commands recieved will in "log.txt" by default and it will be located in the active directory. 
     * If the program is run again the log.txt file will be cleared. The user of this class can pass in a directory path (as a string)
     * into the constructor to save the log file to that directory. The user can also pass in a flag to the constructor that enables
     * the saving of older log files, new files will be "log-[timestamp]".txt
     * 
     * This will not run on Windows CE btw. I don't think anyone will ever try that considering how outdated it is but just
     * b e w a r e.
     * 
     * As for memory location and data handling, by default this class has 10 memory locations, this can be changed by editing
     * the constant variable numMemoryLocations. This stub function generator emulates an FG that has specific "WAVEX" memory
     * locations and not an internal hard disk on which arbitrary files can be saved
     * 
     * Data/waveforms uploaded will be stored in bin files, with the name of the memory location in the folder marked  SFG_DATA
     * which will be created in the active directory if it does not exist. These data files will NOT be timestamped and will be
     * overwritten if the user attempts to upload data to them
     * 
    */
    public class StubFunctionGenerator : IFunctionGenerator
    {
        private readonly string directoryPath;
        private readonly string filePath;
        private const int defaultNumMemoryLocations = 10;
        private const byte defaultNumChannels = 2;
        private const double maximumSupportedVoltage = 10.0;
        private const double minimumSupportedVoltage = -10.0;
        // so we can be more flexible.
        private readonly List<string> validMemLocations;
        private readonly byte numChannels;
        private readonly int numMemoryLocations;
        private readonly WaveformParam[] channelWaveData;  // now arrays DO use zero-based indexing so this is gonna be interesting.

        // I'm not making 16 constructors, if you need something really specific, just use the full one.
        // The default number of channels is 2, and the default number of memory locations is 10.

        /// <summary>
        /// Default constructor, the stub function generator will save the log files in the program directory under "log.txt"
        /// </summary>
        /// <remarks> I'm (probably) not making 16 constructors, if you need something really specific, just use the full one. 
        /// The default number of channels is 2, and the default number of memory locations is 10.</remarks>
        public StubFunctionGenerator() : this(Directory.GetCurrentDirectory(), false, defaultNumMemoryLocations ,defaultNumChannels) { }

        /// <summary>
        /// The constructor that takes in a number of memory locations that the client wants the stub function generator to have
        /// everything else is set to default
        /// </summary>
        /// <param name="numMemoryLocations">The number of memory locations for the StubFunctionGenerator to have</param>
        public StubFunctionGenerator(int numMemoryLocations) : this(Directory.GetCurrentDirectory(), false, numMemoryLocations, defaultNumChannels) { }

        /// <summary>
        /// The constructor that takes in a number of channels that the client wants the stub function generator to have
        /// everything else is set to default.
        /// If you need more than 128 channels then yikes.
        /// </summary>
        /// <param name="numChannels">The number of channels for the SFG to have</param>
        public StubFunctionGenerator(byte numChannels) : this(Directory.GetCurrentDirectory(), false, defaultNumMemoryLocations, numChannels) { }

        /// <summary>
        /// Single parameter constructor, the stub function generator will save the log files in the given directory under "log.txt"
        /// </summary>
        /// <param name="directoryPath">The path to the directory to save the log files in</param>
        public StubFunctionGenerator(string directoryPath) : this(directoryPath, false, defaultNumMemoryLocations, defaultNumChannels) { }

        /// <summary>
        /// The other single parameter constructor, if timeStampedLogFiles is true, the stub function generator will save the log
        /// files in the program directory, but will use timestamps in order not to overwrite previous files. If false, it's the
        /// same as calling the default constructor
        /// </summary>
        /// <param name="timeStampedLogFiles"></param>
        public StubFunctionGenerator(bool timeStampedLogFiles) : this(Directory.GetCurrentDirectory(),timeStampedLogFiles, defaultNumMemoryLocations, defaultNumChannels) { }

        /// <summary>
        /// The two argument constructor. Creates a StubFunctionGenerator object with log files saved in the given directory path
        /// and with timestamped log files, if that option is set
        /// </summary>
        /// <param name="directoryPath">The path to the directory to save log files to</param>
        /// <param name="timeStampedLogFiles">Whether or not to save log files with timestamps, or overwrite previous ones</param>
        public StubFunctionGenerator(string directoryPath, bool timeStampedLogFiles, int numMemoryLocations, byte numChannels)
        {
            this.numMemoryLocations = numMemoryLocations;
            this.numChannels = numChannels;
            channelWaveData = new WaveformParam[numChannels];  // init with a length of however many channels there are.
            string filePath;
            this.directoryPath = directoryPath;  // set the directory to write log files to.
            
            if (timeStampedLogFiles)  // create a file with a timestamp in the name
            {
                string timeStamp = string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now);  // get the current time and
                // format it into a nice string
                filePath = directoryPath + "\\log-" + timeStamp + ".txt";  // then append it to the directory path and
                // make it a .txt file.
            }
            else  // just use log.txt and overwrite what was there previously
            {
                // No multithread lock here, so yeah, clients don't mess with "log.txt" I guess
                filePath = directoryPath + "\\log.txt";
            }
            if (File.Exists(filePath))  // if it already exists (with the timestamp ones this should be impossible)
            {
                File.Delete(filePath);  // delete it
            }
            
            //File.Create(filePath);  // then init the filestream after creating the new file
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {

                streamWriter.WriteLine("StubFunctionGenerator log file, generated at "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            validMemLocations = new List<string>();  // create the valid memory locations
            for (int i = 1; i <= numMemoryLocations; i++)  // WAVE1-WAVE(whatever), no zero based indexing
            {
                validMemLocations.Add("WAVE" + i);
            }
            for(int i = 0; i < numChannels; i++)
            {
                channelWaveData[i] = new WaveformParam();
            }
            this.filePath = filePath;
            string dataDirectory = directoryPath + "\\SFG_DATA";  // set directory path
            Directory.CreateDirectory(dataDirectory);  // creates the directory if it doesn't exist, and does nothing otherwise.
        }

        public void CalibrateWaveform(int channel)
        {
            Console.WriteLine("Calibrating");
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {

                streamWriter.WriteLine("CalibrateWaveform(" + channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            
            
        }

        public byte[] GetAdditionalData(string memoryLocation)
        {
            using (StreamWriter streamWriter = new StreamWriter(this.filePath,true))
            {
                streamWriter.WriteLine("GetAdditionalData(" + memoryLocation + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            string dataDirectory = directoryPath + "\\SFG_DATA";  // set directory path
            string filePath = dataDirectory + "\\" + memoryLocation + "_DATA.bin";  // set file path
            return File.ReadAllBytes(filePath);
        }

        public double GetAmplitude(int channel)
        {
            // gonna have to do channelWaveData[channel-1] for these because of how arrays are zero based indexed, while
            // FG channels are NOT.
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("GetAmplitude(" + channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("Amplitude of channel " + channel + " is " + channelWaveData[channel-1].amplitude);
            }
            return channelWaveData[channel - 1].amplitude;
        }

        public double GetDCOffset(int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("GetDCOffset(" + channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("DCOffset of channel " + channel + " is " + channelWaveData[channel - 1].DCOffset);
            }
            return channelWaveData[channel - 1].DCOffset;
        }

        public double GetFrequency(int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("GetFrequency(" + channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("Frequency of channel " + channel + " is " + channelWaveData[channel - 1].frequency);
            }
            return channelWaveData[channel - 1].frequency;
        }

        public double GetHighLevel(int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("GetHighLevel(" + channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("high-level of channel " + channel + " is " + channelWaveData[channel - 1].highLevel);
            }
            return channelWaveData[channel - 1].highLevel;
        }

        public double GetLowLevel(int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("GetLowLevel(" + channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("low-level of channel " + channel + " is " + channelWaveData[channel - 1].lowLevel);
            }
            return channelWaveData[channel - 1].lowLevel;
        }

        public int GetNumChannels()
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath, true))
            {
                streamWriter.WriteLine("GetNumChannels() "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("number of channels is " + numChannels);
            }
            return numChannels;
        }

        public double GetPhase(int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("GetPhase(" + channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("phase of channel " + channel + " is " + channelWaveData[channel - 1].phase);
            }
            return channelWaveData[channel - 1].phase;
        }

        public double GetSampleRate(int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("GetSampleRate(" + channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("sample rate of channel " + channel + " is " + channelWaveData[channel - 1].sampleRate);
            }
            return channelWaveData[channel - 1].lowLevel;
        }

        public string[] GetValidMemoryLocations()
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath, true))
            {
                streamWriter.WriteLine("GetValidMemoryLocations() "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                
            }
            return validMemLocations.ToArray();
        }

        public string[] GetWaveformList()
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath, true))
            {
                streamWriter.WriteLine("GetWaveformList() "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));

            }
            return validMemLocations.ToArray();
        }

        public WaveformType GetWaveformType(int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("GetWaveformType(" + channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("phase of channel " + channel + " is " + channelWaveData[channel - 1].waveformType.ToString());
            }
            return channelWaveData[channel - 1].waveformType;
        }

        public void LoadWaveform(string name, int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(this.filePath,true))
            {
                streamWriter.WriteLine("LoadWaveform(" + name + ", "+ channel + ") "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            string filePath = directoryPath + "\\SFG_DATA\\" + name + ".bin";  // the path to where the memory location's
            // bin file is stored.
            // now we deserialize the binary-encoded object
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream fs = File.Open(filePath, FileMode.Open);
            object obj = formatter.Deserialize(fs);  // it returns an object
            WaveformParam temp = (WaveformParam)obj;  // we need to cast it so we can access the fields
            fs.Flush();
            fs.Close();
            fs.Dispose();  // clean up
            channelWaveData[channel - 1] = temp;  // just set the waveform param entry for the selected channel to
            // the one we just deserialized from the file.

        }

        public void SetAllOutputsOff()
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetAllOutputsOff() "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
        }

        public void SetAmplitude(double amplitude, int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetAmplitude(" + amplitude + ", " + channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            channelWaveData[channel - 1].amplitude  = amplitude;
        }

        public void SetDCOffset(double DCOffset, int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetDCOffset(" + DCOffset + ", " + channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            channelWaveData[channel - 1].DCOffset = DCOffset;
        }

        public void SetFrequency(double frequency, int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetFrequency(" + frequency + ", " + channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            channelWaveData[channel - 1].frequency = frequency;
        }

        public void SetHighLevel(double highLevel, int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetHighLevel(" + highLevel + ", " + channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            channelWaveData[channel - 1].highLevel = highLevel;
        }

        public void SetLowLevel(double lowLevel, int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetLowLevel(" + lowLevel + ", " + channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            channelWaveData[channel - 1].lowLevel = lowLevel;
        }

        public void SetOutputOff(int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetOutputOff("+ channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
        }

        public void SetOutputOn(int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetOutputOn(" + channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
        }

        public void SetPhase(double phase, int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetPhase(" + phase + ", " + channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            channelWaveData[channel - 1].phase = phase;
        }

        public void SetSampleRate(double sampleRate, int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetSampleRate(" + sampleRate + ", " + channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            channelWaveData[channel - 1].sampleRate = sampleRate;
        }

        public void SetWaveformType(WaveformType type, int channel)
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("SetWaveformType(" + type.ToString() + ", " + channel + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            channelWaveData[channel - 1].waveformType = type;
        }

        public void StoreAdditionalData(byte[] data, string memoryLocation)
        {
            using (StreamWriter streamWriter = new StreamWriter(this.filePath,true))
            {
                streamWriter.WriteLine("StoreAdditionalData(data, " + memoryLocation+") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            string dataDirectory = directoryPath + "\\SFG_DATA";  // set directory path
            Directory.CreateDirectory(dataDirectory);  // creates the directory if it doesn't exist, and does nothing otherwise.
            string filePath = dataDirectory + "\\" + memoryLocation + "_DATA.bin";  // set file path
            File.WriteAllBytes(filePath,data);  // overwrites if the file is already there, and creates one if it is not.
            // that's what we want!

        }

        public void UploadWaveformData(short[] waveData, double sampleRate, double lowLevel, double highLevel, double DCOffset, double phase, string memoryLocation)
        {
            using (StreamWriter streamWriter = new StreamWriter(this.filePath,true))
            {
                streamWriter.WriteLine("UploadWaveformData(short[] waveData, " + memoryLocation + ", " + sampleRate + ", " +
                    lowLevel + ", " + highLevel + ", " + DCOffset + ", " + phase + ", " + memoryLocation + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            WaveformParam temp = new WaveformParam
            {
                amplitude = highLevel - lowLevel,
                DCOffset = DCOffset,
                frequency = sampleRate / waveData.Length,
                lowLevel = lowLevel,
                highLevel = highLevel,
                phase = phase,
                waveformType = WaveformType.ARB  // uploaded waveforms are always of the arbitrary type
            };
            string dataDirectory = directoryPath + "\\SFG_DATA";  // set directory path
            Directory.CreateDirectory(dataDirectory);  // creates the directory if it doesn't exist, and does nothing otherwise.
            string filePath = dataDirectory + "\\" + memoryLocation + ".bin";
            if (File.Exists(filePath))  // if the waveform already exists
            {
                File.Delete(filePath);  // delete it
            }
            Stream ms = File.OpenWrite(filePath);
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, temp);  // serialize the object, and then write it to the file.
            ms.Flush();
            ms.Close();
            ms.Dispose();  // then clean up
        }

        public void UploadWaveformData(double[] voltageArray, double sampleRate, double DCOffset, double phase, string memoryLocation)
        {
            double lowLevel = voltageArray.Min();
            double highLevel = voltageArray.Max();
            using (StreamWriter streamWriter = new StreamWriter(this.filePath,true))
            {
                streamWriter.WriteLine("UploadWaveformData(double[] voltageArray, " + memoryLocation + ", " + sampleRate + ", " +
                    lowLevel + ", " + highLevel + ", " + DCOffset + ", " + phase + ", " + memoryLocation + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            WaveformParam temp = new WaveformParam
            {
                amplitude = highLevel - lowLevel,
                DCOffset = DCOffset,
                frequency = sampleRate / voltageArray.Length,
                lowLevel = lowLevel,
                highLevel = highLevel,
                phase = phase,
                waveformType = WaveformType.ARB  // uploaded waveforms are always of the arbitrary type
            };
            string filePath = directoryPath + "\\SFG_DATA\\" + memoryLocation + ".bin";
            if (File.Exists(filePath))  // if the waveform already exists
            {
                File.Delete(filePath);  // delete it
            }
            Stream ms = File.OpenWrite(filePath);
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, temp);  // serialize the object, and then write it to the file.
            ms.Flush();
            ms.Close();
            ms.Dispose();  // then clean up
        }

        public void WriteRawCommand(string command)
        {
            // you should be aware that since WriteRawCommand and WriteRawQuery are essentially allowed breaks in the abstraction
            // layer of the interface, and exist because function generators can be weird, and hell, might even be removed
            // in some version of the interface, clearly writing an SCPI command to something like this isn't going to actually
            // do the thing that you want. I'm not writing an SCPI emulator here (that'd be interesting though)
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("WriteRawCommand("+command +") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
        }

        /// <summary>
        /// A stub implementation of writing an SCPI query to the function generator. Doesn't work except for "*IDN?" requests
        /// </summary>
        /// <param name="query">the query to write to the generator</param>
        /// <returns>The string "response" if the query is not "*IDN?" otherwise the identity of the generator</returns>
        public string WriteRawQuery(string query)
        {
            // for this there's really nothing I can reply with. However I can make ONE exception to my above explanation about how
            // these don't do anything. The "*IDN?" query is quite common. Luckily, if I do end up removing these from the interface,
            // nothing breaks here because it's not extending an abstract class. These will simply become class only methods.
            // But yes, "*IDN?" does work.
            using (StreamWriter streamWriter = new StreamWriter(filePath,true))
            {
                streamWriter.WriteLine("WriteRawQuery(" + query + ") "  // can't forget that space
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
            }
            if(query.ToUpper() == "*IDN?" || query.ToUpper() == "IDN?")
            {
                return "BRL Technologies,RM-2048X,CSE08092003PAllen,0.123-09.27.2017-1.33-77-00";  // return a SCPI '99 complient
                // identification string
            } else
            {
                return "response";  // don't return an empty string as it could possibly break something
            }
        }

        public double GetMaxSupportedVoltage()
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath, true))
            {
                streamWriter.WriteLine("GetMaxSupportedVoltage() "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("Max supported voltage is " + maximumSupportedVoltage);

            }
            return maximumSupportedVoltage;
        }

        public double GetMinSupportedVoltage()
        {
            using (StreamWriter streamWriter = new StreamWriter(filePath, true))
            {
                streamWriter.WriteLine("GetMinSupportedVoltage() "
                        + string.Format("{0:yyyy-MM-dd_hh-mm-ss-fff}", DateTime.Now));
                streamWriter.WriteLine("Min supported voltage is " + minimumSupportedVoltage);

            }
            return minimumSupportedVoltage;
        }

        public string GetIdentificationString()
        {
            return "BRL Technologies,RM-2048X,CSE08092003PAllen";
        }

        public string GetModelString()
        {
            return "BRL Technologies,RM-2048X";
        }

        public ConnectionType GetConnectionType()
        {
            return ConnectionType.OTHER_RESERVED;   // currently set to OTHER_RESERVED, probably better than pretending to be connected over USB
        }

        public DeviceType GetDeviceType()
        {
            return DeviceType.Function_Generator;
        }

        [Serializable()]
        class WaveformParam
        {
            public double amplitude;
            public double frequency;
            public double sampleRate;
            public double lowLevel;
            public double highLevel;
            public double DCOffset;
            public double phase;
            public WaveformType waveformType;
        }
    }
}
