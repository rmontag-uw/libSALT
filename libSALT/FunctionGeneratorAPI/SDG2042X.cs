/* Siglent SDG2042X Function Generator API Implementation
 * S.A.L.T Project Library
 * Written by Maurice Montag, 2019
 * BioRobotics Lab, University of Washington, Seattle
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace libSALT.FunctionGeneratorAPI
{
    /// <summary>
    /// This class represents the Siglent SDG2042X Arbitrary Waveform Generator. It implements the IFunctionGenerator interface, and extends
    /// the VISAFunctionGenerator class, using its default implementation for I/O and other operations.
    /// </summary>
    public class SDG2042X : VISAFunctionGenerator
    {
        private readonly List<string> validMemLocations;
        private const int numOfWaveFormsAllowed = 10;  // a constant for how many waveforms we wish to allow.
        private const double maximumSupportedVoltage = 10.0;
        private const double minimumSupportedVoltage = -10.0;
        private const int numberOfChannels = 2;  // put this as a const so we don't have magic numbers anywhere
        private const string ModelResponseString = "Siglent Technologies,SDG2042X";

        public SDG2042X(string visaID) // we take in two arguments here. 

           : base(visaID, numberOfChannels)

        {
            //WriteRawCommand("*ESE 1");  // flag the event status register
            validMemLocations = new List<string>();  // guess we don't use ArrayList in C#
            for (int i = 1; i <= numOfWaveFormsAllowed; i++)  // WAVE1-WAVE(whatever), no zero based indexing
            {
                validMemLocations.Add("WAVE" + i);
            }
            /* 
             * okay this is gonna be an interesting exercise.
             * There's no way to actually delete files directly from the SCPI interface.
             * but we can use the virtual key press feature to pretend to delete them.
             * Also we don't know the size of the internal memory.
             * I also don't know what happens when we run out of space.
             * So we're going to do a little work-around of that. We will pretend to have a fixed number of waveform spaces,
             * just like generators without a mass storage device. We can increase this number as needed.
             */
        }
        public override void CalibrateWaveform(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":BSWV WVTP,SINE,AMP,.5V,FRQ,25,OFST,0V,PHSE,0");  // generate a 500 Hz, 1Vpp Sine wave on the given channel
            SetOutputOn(channel);  // turn on the channel after calibration waveform is created
        }

        public override string[] GetValidMemoryLocations()
        {
            return validMemLocations.ToArray();  // convert the List<string> to an array before returning
        }

        public override string[] GetWaveformList()
        {
            return GetValidMemoryLocations();
        }

        public override void LoadWaveform(string name, int channel)
        {
            CheckChannelParam(channel);  // check for valid channel
            SetIOTimeout(30000);  // increase timeout to 30 seconds
            //C2:SRATE MODE,TARB
            WriteRawCommand("C" + channel + ":SRATE MODE,TARB");  // set the generator to truarb mode before loading files
            if (!Array.Exists(GetValidMemoryLocations(), element => element.Equals(name)))  // check to see if the requested memory
                                                                                            // location exists or not
            {  // if it doesn't exist
                throw new FileNotFoundException(name + "does not exist in memory");  // throw a FileNotFoundException
            }
            WriteRawCommand("C" + channel + ":ARWV NAME," + name);  // load the requested waveform into the channel.
            WriteRawCommand("*CLS");  // clear status
            WriteRawCommand("*OPC");  // wait until operation is complete
            byte ESRvalue = 0;
            while ((ESRvalue & 1) == 0)
            {
                ESRvalue = byte.Parse(WriteRawQuery("*ESR?"));
                Thread.Sleep(500);
            }
            SetIOTimeout(2000);  // set timeout back to 2 seconds
        }

        public override void SetOutputOff(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":OUTP OFF");  // e.g: C1:OUTP OFF
        }

        public override void SetOutputOn(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":OUTP ON"); // e.g: C2:OUTP ON
        }

        public override void SetFrequency(double frequency, int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":BSWV FRQ," + frequency);
        }

        public override double GetFrequency(int channel)
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery("C" + channel + ":BSWV?");
            SDGResponse r = new SDGResponse(response);
            return r.Frequency;
            //C1:BSWV WVTP,SINE,FRQ,60HZ,PERI,0.0166667S,AMP,4V,AMPVRMS,1.414Vrms,OFST,0V,HLEV,2V,LLEV,-2V,PHSE,0

        }

        public override void SetAmplitude(double amplitude, int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":BSWV AMP," + amplitude);
        }

        public override double GetAmplitude(int channel)
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery("C" + channel + ":BSWV?");
            SDGResponse r = new SDGResponse(response);
            return r.Amplitude;
        }

        public override void SetDCOffset(double dcOffset, int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":BSWV OFST," + dcOffset);
        }

        public override double GetDCOffset(int channel)
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery("C" + channel + ":BSWV?");
            SDGResponse r = new SDGResponse(response);
            return r.DCOffset;
        }

        public override void UploadWaveformData(double[] voltageArray, double sampleRate, double DCOffset, double phase, string memoryLocation)
        {
            if (voltageArray.Max() > 10)
            {
                throw new ArgumentException("Requested maximum voltage is too high for this function generator");
            }
            if (voltageArray.Min() < -10)
            {
                throw new ArgumentException("Requested minimum voltage is too low for this function generator");
            }
            if (voltageArray.Length > 8000000)  // check if the input data has too many points
            {
                throw new ArgumentException("Too many points for this function generator");
            }
            if (!Array.Exists(GetValidMemoryLocations(), element => element.Equals(memoryLocation)))  // mem location checking
            {
                throw new ArgumentException("requested memory location does not exist on this function generator");
            }
            //Console.WriteLine("DC OFFSET OF THIS SIGNAL IS " + voltageArray.Sum() / voltageArray.Length);
            double maxVoltage = voltageArray.Max();  // get the max and min voltage
            //Console.WriteLine("MAX VOLTAGE IS " + maxVoltage);
            double minVoltage = voltageArray.Min();  // no need to check if max voltage > min voltage.
            //Console.WriteLine("MIN VOLTAGE IS " + minVoltage);
            double voltageDiff = maxVoltage - minVoltage;
            //Console.WriteLine("VOLTAGE DIFF IS " + voltageDiff);
            // we could have it truncate and continue, but fail-fast behavior is the ideal
            if (voltageDiff > 20)  // some error checking, 20Vpp is the most this function generator can handle
            {
                throw new ArgumentException("Voltages are out of range of the SDG2042X, maximum supported amplitude is 20Vpp");
            }
            List<short> waveData = new List<short>();  // get a list<short>

            foreach (double d in voltageArray)  // map the voltages to 16 bit short values

            {
                double scaledVoltage = (d - minVoltage) / voltageDiff;  // scaled to a fraction of 1
                short temp = (short)(-32768 + (scaledVoltage * 65535));  // then mapped onto the 16 bit range of short values
                waveData.Add(temp);
            }

            short[] toReturn = waveData.ToArray();
            UploadWaveformData(toReturn, sampleRate, minVoltage, maxVoltage, DCOffset, phase, memoryLocation);
        }

        public override void UploadWaveformData(short[] waveData, double sampleRate, double lowLevel, double highLevel, double DCOffset, double phase, string memoryLocation)
        {
            if (waveData.Length > 8000000)  // check if the input data has too many points
            {
                throw new ArgumentException("Too many points for this function generator");
            }
            if (waveData.Length < 8)
            {
                throw new ArgumentException("Too few points for this function generator");
            }
            if (!Array.Exists(GetValidMemoryLocations(), element => element.Equals(memoryLocation)))
            {
                throw new ArgumentException("requested memory location does not exist on this function generator");
            }
            if (highLevel < lowLevel)
            {
                throw new ArgumentException("highLevel must be greater than lowLevel");
            }
            if (!BitConverter.IsLittleEndian)  // The function generator expects any sent data to be little-endian encoded
                                               // so if the data is in big endian we have to swap the ordering.
            {
                List<short> swap = new List<short>();
                byte[] byteSwap = new byte[2];
                foreach (short sh in waveData)  // iterate over all the shorts in waveform data
                {
                    byteSwap = BitConverter.GetBytes(sh);
                    Array.Reverse(byteSwap);  // and swap their bytes
                    swap.Add(BitConverter.ToInt16(byteSwap, 0));  // before putting them back in the array
                }
                waveData = swap.ToArray();  // and using that array instead.
            }

            double amplitude = highLevel - lowLevel;
            // Console.WriteLine("Amplitude is " + amplitude);
            // Console.WriteLine("DC offset is " + DCOffset);
            int numSamples = waveData.Length;  // get the number of samples
            byte[] byteFormWaveData = new byte[waveData.Length * sizeof(short)];  // create a new array of bytes
            Buffer.BlockCopy(waveData, 0, byteFormWaveData, 0, byteFormWaveData.Length);  // and copy the shorts into it 
            // (after splitting them)
            // the problem with this is that while it uploads the data and doesn't load in the new waveform, it sets the freq, phase, and offset
            // to the ones stored in the new waveform, even if that's really not what was asked for.
            string currentWaveData = WriteRawQuery("C1:BSWV?");
            double currentSampleRate = 400;  // probably shouldn't even take the chance of having it be 0.
            if (GetWaveformType(1) == WaveformType.ARB)
            {
                currentSampleRate = GetSampleRate(1);
            }
            string commandToWrite = "C1:WVDT WVNM," + memoryLocation + ",FREQ," +
            (sampleRate / numSamples) + ",AMPL," + amplitude + ",OFST," + DCOffset + ",PHASE," + phase + ",WAVEDATA,";
            // string commandToWrite = "C1:WVDT WVNM," + memoryLocation + ",FREQ," +
            // (sampleRate / numSamples) + ",LLEV," + lowLevel + ",HLEV," + highLevel+ ",PHASE," + phase + ",WAVEDATA,";
            // string part of the command to write.
            List<byte> byteList = new List<byte>();  // create a List of bytes
            byteList.AddRange(Encoding.ASCII.GetBytes(commandToWrite));  // add the ASCII encoded string to the list of bytes
            byteList.AddRange(byteFormWaveData);  // then add the waveform data
            SetIOTimeout(30000);
            WriteRawData(byteList.ToArray());
            WriteRawCommand(currentWaveData);
            if (GetWaveformType(1) == WaveformType.ARB)  // if there's any issue with the interface with unknown timeout errors and the like
                                                         // it's gonna be from this code right here. Also any weird issues with parameters changing on upload.
                                                         // that's all going to be from here.
            {
                SetSampleRate(currentSampleRate, 1);
            }
            SetIOTimeout(2000);
        }


        public override void SetSampleRate(double sampleRate, int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":SRATE MODE,TARB,VALUE," + sampleRate);
        }

        public override double GetSampleRate(int channel)  // luckily this command is different, so it doesn't need to work with the
                                                           // response parser class
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery("C" + channel + ":SRATE?");
            string[] keys = response.Split(',');  // split into tokens
            if (keys.Length < 4)  // if the generator is not in direct sampling mode, return -1
            {
                return -1;
            }
            if (keys[1] == "DDS")  // if the generator is not in direct sampling (TruArb) mode return -1
            {
                return -1;
            }
            {

                string samplerate = keys[3];
                samplerate = samplerate.Substring(0, samplerate.Length - 5);  // get the sample rate data
                return double.Parse(samplerate);  // parse it into a double and return
            }
        }

        public override void SetPhase(double phase, int channel)
        {
            //C1:BSWV WVTP,SINE,FRQ,60HZ,PERI,0.0166667S,AMP,4V,AMPVRMS,1.414Vrms,OFST,0V,HLEV,2V,LLEV,-2V,PHSE,0
            //C1:BSWV WVTP,ARB,FRQ,9.375HZ,PERI,0.106667S,AMP,4V,OFST,0V,HLEV,2V,LLEV,-2V,PHSE,0
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":BSWV PHSE," + phase);
        }

        public override double GetPhase(int channel)
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery("C" + channel + ":BSWV?");
            SDGResponse r = new SDGResponse(response);
            return r.Phase;
        }

        public override void SetHighLevel(double highLevel, int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":BSWV HLEV," + highLevel + "V");
        }

        public override double GetHighLevel(int channel)
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery("C" + channel + ":BSWV?");
            SDGResponse r = new SDGResponse(response);
            return r.HighLevel;
        }

        public override void SetLowLevel(double lowLevel, int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("C" + channel + ":BSWV LLEV," + lowLevel + "V");
        }

        public override double GetLowLevel(int channel)
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery("C" + channel + ":BSWV?");
            SDGResponse r = new SDGResponse(response);
            return r.LowLevel;
        }

        public override void SetWaveformType(WaveformType waveType, int channel)
        {
            // all waveform types in the enum are specified for this function generator
            CheckChannelParam(channel);
            int waveNum = (int)waveType;
            string waveName = Enum.GetName(typeof(WaveformType), waveNum);
            WriteRawCommand("C" + channel + ":BSWV WVTP," + waveName);
        }

        public override WaveformType GetWaveformType(int channel)
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery("C" + channel + ":BSWV?");
            SDGResponse r = new SDGResponse(response);
            return r.Wavetype;
        }

        public override void StoreAdditionalData(byte[] data, string memoryLocationName)
        {
            string commandToWrite = "C1" + ":WVDT WVNM," + memoryLocationName + "_data,WAVEDATA" + " ";  // generic wave stuff
            StringBuilder sb = new StringBuilder(commandToWrite);
            foreach (byte b in data)
            {
                sb.Append((char)b);
            }
            WriteRawCommand(sb.ToString());
        }

        public override byte[] GetAdditionalData(string memoryLocationName)
        {
            string encodedData = WriteRawQuery("WVDT? USER," + memoryLocationName + "_data");
            int index = encodedData.IndexOf("WAVEDATA");
            encodedData = encodedData.Substring(index + 9, encodedData.Length - (index + 10));
            byte[] bytes = Encoding.UTF8.GetBytes(encodedData);
            return bytes;
        }

        public override double GetMaxSupportedVoltage()
        {
            return maximumSupportedVoltage;
        }

        public override double GetMinSupportedVoltage()
        {
            return minimumSupportedVoltage;
        }

        public override string GetModelString()
        {
            return ModelResponseString;
        }

        public override ConnectionType GetConnectionType()
        {
            return ConnectionType.VISA_USB;
        }

        /// <summary>
        /// This class represents the data returned from the function generator about the waveform for a channel
        /// used to simplify data parsing (if you can believe it)
        /// It will allow the creation of additional getters and setters if needed.
        /// </summary>
        protected class SDGResponse
        {
            //C1:BSWV WVTP,SINE,FRQ,22.22222222HZ,PERI,0.045S,AMP,4V,AMPVRMS,1.414Vrms,OFST,0V,HLEV,2V,LLEV,-2V,PHSE,0
            // things that are likely to exist are set to 0 originally, the ones that will likely never be relevant are set to -1 
            public int Channel { get; }
            public WaveformType Wavetype { get; }
            public double Frequency { get; }
            public double Period { get; }
            public double Amplitude { get; }
            public double DCOffset { get; }
            public double Symmetry { get; }  // -1
            public double Dutycycle { get; } // -1
            public double Phase { get; }
            public double Stdev { get; }// -1
            public double Mean { get; } // -1
            public double Width { get; } // -1
            public double Rise { get; } // -1
            public double Fall { get; } // -1
            public double Delay { get; } // -1
            public double HighLevel { get; }
            public double LowLevel { get; }
            public string Bandstate { get; }  // yes I know the only options are "ON" or "OFF" but usually 
            // it just doesn't exist so...
            public double Bandwidth { get; }  // -1;
            public SDGResponse(string queryResponse)
            {
                Symmetry = -1;
                Dutycycle = -1;
                Stdev = -1;
                Mean = -1;
                Width = -1;
                Rise = -1;
                Fall = -1;
                Delay = -1;
                Bandwidth = -1;
                Channel = int.Parse(queryResponse.Substring(1, 1));  // set the channel.
                List<string> tokens = new List<string>();
                string responseString = queryResponse.Substring(8, queryResponse.Length - 8);  // trim channel number and BSWV command
                MatchCollection matches = Regex.Matches(responseString, "[^,]+,[^,]+,*");
                foreach (Match item in matches)
                {
                    tokens.Add(item.Value.Substring(0, item.Value.Length - 1));
                }
                foreach (string s in tokens)
                {
                    string[] keyvalue = s.Split(',');
                    string key = keyvalue[0];
                    string value = keyvalue[1];
                    // a massive block of switch statements is the proper way to do this suprisingly enough.
                    // the number of responses and the form of these responses is variable depending on what mode the generator is in
                    // for the sake of robustness, this was the only option. (const dictionary is not a thing)
                    switch (key)
                    {
                        case "WVTP":
                            Wavetype = (WaveformType)Enum.Parse(typeof(WaveformType), value); //ParseWaveformEnum(value);
                            break;
                        case "FRQ":
                            Frequency = double.Parse(value.Substring(0, value.Length - 2));  // remove the "Hz" from the end
                            break;
                        case "PERI":
                            Period = double.Parse(value.Substring(0, value.Length - 1));  // remove the "S" from the end
                            break;
                        case "AMP":
                            Amplitude = double.Parse(value.Substring(0, value.Length - 1));  // remove the "V" from the end
                            break;
                        case "OFST":
                            DCOffset = double.Parse(value.Substring(0, value.Length - 1));  // remove the "V" from the end
                            break;
                        case "SYM":
                            Symmetry = double.Parse(value.Substring(0, value.Length));
                            break;
                        case "DUTY":
                            Dutycycle = double.Parse(value.Substring(0, value.Length));
                            break;
                        case "PHSE":
                            Phase = double.Parse(value.Substring(0, value.Length));
                            break;
                        case "STDEV":
                            Stdev = double.Parse(value.Substring(0, value.Length - 1));
                            break;
                        case "MEAN":
                            Mean = double.Parse(value.Substring(0, value.Length - 1));
                            break;
                        case "WIDTH":
                            Width = double.Parse(value.Substring(0, value.Length));
                            break;
                        case "RISE":
                            Rise = double.Parse(value.Substring(0, value.Length - 1), NumberStyles.Float);  // remove the S
                            // NumberStyles.Float is required for parsing the string in exponent formed that is returned
                            break;
                        case "FALL":
                            Fall = double.Parse(value.Substring(0, value.Length - 1), NumberStyles.Float);  // remove the S
                            break;
                        case "DLY":
                            Delay = double.Parse(value.Substring(0, value.Length));
                            break;
                        case "HLEV":
                            HighLevel = double.Parse(value.Substring(0, value.Length - 1));  // remove the V
                            break;
                        case "LLEV":
                            LowLevel = double.Parse(value.Substring(0, value.Length - 1));  // remove the V
                            break;
                        case "BANDSTATE":
                            Bandstate = value;
                            break;
                        case "BANDWIDTH":
                            Bandwidth = double.Parse(value.Substring(0, value.Length - 2));
                            break;
                        default:
                            break; // do nothing, it's probably that VMRS thing (heh)
                    }
                }

            }
        }
    }
}
