using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NationalInstruments.Visa;

namespace TestingPlatformLibrary.OscilloscopeAPI
{
    public class DS1104Z : VISAOscilloscope
    {
        // for some reason this scope prefers its SCPI commands to be in lowercase form, instead of the usual uppercase versions
        private const int numberOfChannels = 4;  // the rigol 1054z has 4 channels.
        private const double minVoltageScale = .01;  // 10 mV
        private const double maxVoltageScale = 100;
        private const double minTimeScale = 0.000000005;  // hmmm this is getting into floating point error territory
        private const double maxTimeScale = 50;
        private const double voltageOffsetScaleConstant = 8;
        private const double triggerOffsetScaleConstant = 5;
        private const double timeOffsetScaleConstant = 10;
        private const string ModelString = "RIGOL TECHNOLOGIES,DS1104Z";
        private readonly double[] voltageScalePresets = new[] { .01, .02, .05, .1, .2, .5, 1, 2, 5, 10, 20, 50, 100 };
        private readonly string[] voltageScalePresetStrings = new[] {"10mV","20mV", "50mV", "100mV", "200mV",
            "500mV", "1V", "2V", "5V", "10V", "50V", "100V"};
        private readonly string[] timeScalePresetStrings = new[] {"5ns", "10ns", "20ns", "50ns", "100ns",
            "200ns", "500ns", "1us", "2us", "5us", "10us", "20us" , "50us", "100us",
            "200us", "500us", "1ms", "2ms", "5ms", "10ms", "20ms", "50ms", "100ms", "200ms", "500ms", "1s", "2s", "5s", "10s", "20s", "50s"};
        private readonly double[] timeScalePresets = new[] { 0.000000005, 0.000000010, 0.000000020, 0.000000050, 0.000000100,
            0.000000200, 0.000000500, 0.000001000, 0.000002000, 0.000005000, 0.000010000, 0.000020000, 0.00005, 0.00010, 0.00020,
            0.00050, 0.00100, 0.00200, 0.00500, .01, .02, .05, .1, .2, .5, 1, 2, 5, 10, 20, 50};
        private readonly int[] channelOneOnlyAllowedMemDepth = new[] { 12000, 120000, 1200000, 12000000, 24000000 };
        private readonly int[] dualChannelAllowedMemDepth = new[] { 6000, 60000, 600000, 6000000, 12000000 };
        private readonly int[] threeAndFourChannelAllowedMemDepth = new[] { 3000, 30000, 300000, 3000000, 6000000 };
        private readonly HashSet<int> enabledChannels;
        public DS1104Z(string visaID, ResourceManager rm)

             : base(visaID, rm, numberOfChannels)
        {
            enabledChannels = new HashSet<int>();
        }  // might need to put some commands to run here on startup, we'll see

        public override void AutoScale()
        {
            WriteRawCommand(":AUT");  // autoscales the waveform, and enables channels with data on them (for better or worse)
        }

        public override void DisableChannel(int channel)
        {
            CheckChannelParam(channel);
            enabledChannels.Remove(channel);
            WriteRawCommand(":CHANnel" + channel + ":DISPlay OFF");  // turn off the given channel
        }

        public override void EnableChannel(int channel)
        {
            CheckChannelParam(channel);
            enabledChannels.Add(channel);
            WriteRawCommand(":CHANnel" + channel + ":DISPlay ON");  // turn on the given channel
        }

        public override double[] GetWaveVoltages(int channel)
        {
            byte[] rawByteData = GetWaveData(channel);  // this will set the active channel to channel
            double YOrigin = GetYOrigin();  // so then we don't need to set the active channel for these guys
            double Yinc = GetYIncrement();
            double Yref = GetYReference();
            double[] voltageArray = rawByteData.Select(dataPoint => ScaleVoltage(dataPoint, YOrigin, Yinc, Yref)).ToArray();
            return voltageArray;
        }

        private double ScaleVoltage(byte dataPoint, double YOrigin, double Yinc, double Yref)
        {
            double voltage = (dataPoint - YOrigin - Yref) * Yinc;
            return voltage;
        }

        public override byte[] GetWaveData(int channel)
        {
            CheckChannelParam(channel);  // check to make sure the channel param makes sense
            WriteRawCommand(":wav:sour chan" + channel);  // set the channel to grab the wavedata from to the one specified
            WriteRawCommand(":wav:mode norm");  // set the waveform capture mode to normal
            WriteRawCommand(":wav:form byte");  // set the waveform format to byte (fastest and can capture the most data at once)
            WriteRawCommand("wav:start 1");
            WriteRawCommand("wav:stop 1200");  // gotta set this so we can still get data after we log to the file
            byte[] rawData = ReadRawData(":wav:data?"); // grab the wave data from the scope by reading it as a byte array

            /*  Data processing. The waveform data read contains the TMC header. The length of the header is 11
                bytes, wherein, the first 2 bytes are the TMC header denoter (#) and the width descriptor (9)
                respectively, the 9 bytes following are the length of the data which is followed by the waveform data
                and the last byte is the terminator (0x0A). Therefore, the effective waveform points read is from the
                12nd to the next to last.
            */
            IEnumerable<byte> temp = rawData.Skip(11);  // we skip the first 11 elements as they are the header and length data
            byte[] toReturn = temp.Take(temp.Count() - 1).ToArray();  // remove the last element (the 0x0A) and convert back to an array
            return toReturn;  // then return the waveform as bytes

        }

        public override double[] GetDeepMemVoltages(int channel)
        {
            byte[] rawData = GetDeepMemData(channel);
            double YOrigin = GetYOrigin();  // so then we don't need to set the active channel for these guys
            double Yinc = GetYIncrement();
            double Yref = GetYReference();
            List<double> toReturn = new List<double>();
            foreach(byte b in rawData)
            {
                toReturn.Add(ScaleVoltage(b, YOrigin, Yinc, Yref));
            }
            return toReturn.ToArray();
        }

        public override byte[] GetDeepMemData(int channel)
        {
            CheckChannelParam(channel);
            SetActiveChannel(channel);
            Stop();
            int memDepth = GetMemDepth();
            if (memDepth == 0)
            {
                throw new ArgumentException("Cannot download deep memory when memory depth is set to auto");
            }
            WriteRawCommand(":wav:sour chan" + channel);  // set the channel to grab the wavedata from to the one specified
            WriteRawCommand(":wav:mode raw");  // set the waveform capture mode to normal
            WriteRawCommand(":wav:form byte");  // set the waveform format to byte (fastest and can capture the most data at once)
            int stopPosition = 10000;
            int startPosition = 1;
            if (memDepth > 250000)
            {
                stopPosition = 250000;
            }
            else
            {
                stopPosition = memDepth;
            }
            List<byte> buffer = new List<byte>();  // are append operations faster then doing it with pure arrays??? who knows?
            while (buffer.Count < memDepth && startPosition < stopPosition)
            {
                WriteRawCommand("wav:start " + startPosition);
                WriteRawCommand("wav:stop " + stopPosition);
                byte[] rawData = ReadRawData("wav:data?", 5000);
                IEnumerable<byte> temp = rawData.Skip(11);  // we skip the first 11 elements as they are the header and length data
                byte[] pruned = temp.Take(temp.Count() - 1).ToArray();  // remove the last element (the 0x0A) and convert back to an array
                buffer.AddRange(pruned);
                startPosition += pruned.Length;
                if (stopPosition + pruned.Length > memDepth)
                {
                    stopPosition = memDepth;
                }
                else
                {
                    stopPosition += pruned.Length;
                }
            }
           // Console.WriteLine(buffer.Count);
            return buffer.ToArray();
        }


        public override double GetYScale(int channel)
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery("CHANnel" + channel + ":SCALe?");

            //WriteRawCommand(":wav:sour chan" + channel);  // set the channel to grab the wavedata from to the one specified
            //string response = WriteRawQuery(":WAV:YINC?");
            return double.Parse(response, System.Globalization.NumberStyles.Float);  // double.parse can do scientific notation yay

        }


        public override bool IsChannelEnabled(int channel)
        {
            CheckChannelParam(channel);
            string response = WriteRawQuery(":CHANnel" + channel + ":DISP?");
            if (response.Equals("1\n"))  // this is gross, but such is the VISA lifestyle
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override void Run()
        {
            WriteRawCommand(":run");
        }

        public override void Stop()
        {
            WriteRawCommand(":stop");
        }

        protected override byte[] ReadRawData(string query)
        {
            lock (threadLock)
            {
                mbSession.FormattedIO.WriteLine(query);
                return mbSession.RawIO.Read(3000);
            }

        }

        public override double GetYIncrement(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand(":wav:sour chan" + channel);  // set the channel to grab the wavedata from to the one specified
            return GetYIncrement();
        }

        public double GetYOrigin(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand(":wav:sour chan" + channel);  // set the channel to grab the wavedata from to the one specified
            return GetYOrigin();
        }

        public double GetYReference(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand(":wav:sour chan" + channel);  // set the channel to grab the wavedata from to the one specified
            return GetYReference();
        }

        public override Color GetChannelColor(int channel)
        {
            CheckChannelParam(channel);
            switch (channel)
            {
                case 1:
                    return Color.Gold;
                case 2:
                    return Color.Cyan;
                case 3:
                    return Color.DarkViolet;
                case 4:
                    return Color.RoyalBlue;
                default:
                    return Color.Black;  // we should never reach this

            }
        }

        public override void SetActiveChannel(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand(":wav:sour chan" + channel);  // set the channel to grab the wavedata from to the one specified
        }

        public override int GetActiveChannel()
        {
            // query response looks like: "CHAN2"
            return int.Parse(WriteRawQuery("wav:sour?").Substring(3, 1));
        }

        public override double GetYScale()
        {
            string response = WriteRawQuery(":WAV:YINC?");
            return double.Parse(response, System.Globalization.NumberStyles.Float) * 25;  // double.parse can do scientific notation yay
                                                                                          // for whatever reason, the scope returns 1/25th the actual voltage scale value
        }

        public override double GetYIncrement()
        {
            string response = WriteRawQuery(":WAV:YINC?");
            return double.Parse(response, System.Globalization.NumberStyles.Float);
        }

        public double GetYOrigin()
        {
            string response = WriteRawQuery(":WAV:YOR?");
            return double.Parse(response, System.Globalization.NumberStyles.Float);
        }

        public double GetYReference()
        {
            string response = WriteRawQuery(":WAV:YREF?");
            return double.Parse(response, System.Globalization.NumberStyles.Float);
        }

        public override double GetXAxisScale()
        {
            return double.Parse(WriteRawQuery(":TIMebase:MAIN:SCALe?"));
        }

        public override void SetYScale(int channel, double voltageScale)
        {
            CheckChannelParam(channel);
            WriteRawCommand("CHANnel" + channel + ":SCALe " + voltageScale);
        }

        public override double GetMinimumVoltageScale()
        {
            return minVoltageScale;
        }

        public override double GetMinimumTimeScale()
        {
            return minTimeScale;
        }

        public override double GetMaximumVoltageScale()
        {
            return maxVoltageScale;
        }

        public override double GetMaximumTimeScale()
        {
            return maxTimeScale;
        }

        public override double[] GetVoltageScalePresets()
        {
            return voltageScalePresets;
        }

        public override double[] GetTimeScalePresets()
        {
            return timeScalePresets;
        }

        public override string[] GetVoltageScalePresetStrings()
        {
            return voltageScalePresetStrings;
        }

        public override string[] GetTimeScalePresetStrings()
        {
            return timeScalePresetStrings;
        }

        public override void SetXAxisScale(double timeScale)
        {
            WriteRawCommand(":TIMebase:MAIN:SCALe " + timeScale);
        }

        public override double GetYAxisOffset(int channel)
        {
            //:CHANnel<n>:OFFSet
            string response = WriteRawQuery(":CHANnel" + channel + ":OFFSet?");
            return double.Parse(response);
        }

        public override void SetYAxisOffset(int channel, double offset)
        {
            WriteRawCommand(":CHANnel" + channel + ":OFFSet " + offset);
        }

        public override double GetXAxisOffset()
        {
            return double.Parse(WriteRawQuery(":TIMebase:MAIN:OFFSet?"));
        }

        public override void SetXAxisOffset(double offset)
        {
            WriteRawCommand(":TIMebase:MAIN:OFFSet " + offset);
        }

        public override double GetTriggerLevel()
        {
            return double.Parse(WriteRawQuery(":TRIGger:EDGe:LEVel?"));
        }

        public override void SetTriggerLevel(double voltageLevel)
        {
            WriteRawCommand(":TRIGger:EDGe:LEVel " + voltageLevel);
        }

        public override int GetMemDepth()
        {
            string response = WriteRawQuery(":ACQuire:MDEPth?");
            int toReturn;
            if (!int.TryParse(response, out toReturn))  // if it can't parse correctly, then it's AUTO
            {
                return 0;
            }
            return toReturn;
        }

        public override void SetMemDepth(int points)
        {
            if (points == 0)  // if points is 0 we set the scope back to auto mode.
            {
                WriteRawCommand(":ACQuire:MDEPth AUTO");
                return;
            }
            if (!GetAllowedMemDepths().Contains(points))
            {
                throw new ArgumentOutOfRangeException("Requested memory depth not in range");
            }

            WriteRawCommand(":ACQuire:MDEPth " + points);
        }

        public override int[] GetAllowedMemDepths()
        {
            if(enabledChannels.Count() == 1)
            {
                if (enabledChannels.Contains(1))  // it's a weird quirk that we can only use the large memdepths when only channel 1 is enabled
                {
                    return channelOneOnlyAllowedMemDepth;
                }
                return dualChannelAllowedMemDepth;  // we have to use the "dual channel" memory depth setting if any of the others are enabled, even if it's just channel 2 for
                    // example.
            }
            if(enabledChannels.Count() == 2)  // this is so odd. If channel 1 is enabled, then the memory depth is twice as much as it would be otherwise for that number if channel
                // 1 was not enabled
            {
                if (enabledChannels.Contains(1))
                {
                    return dualChannelAllowedMemDepth;
                } else
                {
                    return threeAndFourChannelAllowedMemDepth;
                }
            } else
            {
                return threeAndFourChannelAllowedMemDepth;
            }
        }

        public override void Single()
        {
            WriteRawCommand(":SINGle");
        }

        public override string GetModelString()
        {
            return ModelString;
        }

        public override ConnectionType GetConnectionType()
        {
            return ConnectionType.VISA_USB;
        }

        public override double GetXIncrement(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand(":wav:sour chan" + channel);  // set the channel to grab the wavedata from to the one specified
            return double.Parse(WriteRawQuery(":WAVeform:XINCrement?"));
        }

        public override double GetXIncrement()
        {
            return double.Parse(WriteRawQuery(":WAVeform:XINCrement?"));
        }

        public override double GetYAxisOffsetScaleConstant()
        {
            return voltageOffsetScaleConstant;
        }

        public override double GetTriggerPositionScaleConstant()
        {
            return triggerOffsetScaleConstant;
        }

        public override double GetXAxisOffsetScaleConstant()
        {
            return timeOffsetScaleConstant;
        }
    }
}
