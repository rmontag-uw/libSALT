using NationalInstruments.Visa;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace TestingPlatformLibrary.FunctionGeneratorAPI
{
    class TektronixAFG2021 : VISAFunctionGenerator
    {
        // essentially everything here is unimplemented, also this function generator didn't do what was required for our testing.
        // Continue at your own risk.
        private readonly string[] validMemLocations;
        /*
         * Must call the superclass constructor with the parameters passed in here
         */
        public TektronixAFG2021(string visaID, ResourceManager rm, int channelNum) // we take in two arguments here. 
            : base(visaID, rm, channelNum)
        {
            validMemLocations = new[] { "USER1", "USER2", "USER3", "USER4" };
        }  // but since the AFG2021 always only has one channel, we can pass 1 into the base 
        // class constructor, and not let any clients try to mess with stuff by switching that.

        public override void CalibrateWaveform(int channel)
        {
            CheckChannelParam(channel);  // check that the requested channel is correct
            WriteRawCommand("SOURce1:FUNCtion:SHAPe SIN");  // set the source function shape to sine wave
            WriteRawCommand(":SOURCE:FREQUENCY 500Hz");  // set the frequency to 500 Hz
            WriteRawCommand("SOURce1:VOLTage:LEVel:IMMediate:AMPLitude 1V");  // set the amplitude to 1 Vpp.
            SetOutputOn(channel);  // and turn on the output
        }

        public void ClearWaveform(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("SOUR:FUNC:SHAPE:ARB");  // set the source function shape to arbitrary
            WriteRawCommand("DATA:DEF EMEM");  // clear the edit memory of the function generator
        }

        public override byte[] GetAdditionalData(string memoryLocation)
        {
            throw new NotImplementedException();
        }

        public override double GetAmplitude(int channel)
        {
            throw new NotImplementedException();
        }

        public override ConnectionType GetConnectionType()
        {
            throw new NotImplementedException();
        }

        public override double GetDCOffset(int channel)
        {
            throw new NotImplementedException();
        }

        public override double GetFrequency(int channel)
        {
            throw new NotImplementedException();
        }

        public override double GetHighLevel(int channel)
        {
            throw new NotImplementedException();
        }

        public override double GetLowLevel(int channel)
        {
            throw new NotImplementedException();
        }

        public override double GetMaxSupportedVoltage()
        {
            throw new NotImplementedException();
        }

        public override double GetMinSupportedVoltage()
        {
            throw new NotImplementedException();
        }

        public override string GetModelString()
        {
            throw new NotImplementedException();
        }

        public override double GetPhase(int channel)
        {
            throw new NotImplementedException();
        }

        public override double GetSampleRate(int channel)
        {
            throw new NotImplementedException();
        }

        public override string[] GetValidMemoryLocations()
        {
            return validMemLocations;
        }

        public override string[] GetWaveformList()  // gets a list of waveforms stored in the onboard memory
        {
            /* 
             * Okay there is some ancient ASCII weirdness going on here.
             * the final character in the last string is not the '"' character, but actually ASCII value 10, or
             * the backspace character. So we actually need to remove the final two characters for the last value in the
             * array. Stuff like this is why we can't have nice things, and another reason why we can't write a standardized
             * implementation of GetWaveFormList().
             */
            string response = WriteRawQuery("DATA:CAT?");  
            // this returns a string of different memory locations with saved waveforms.
            string[] waveformList = response.Split(',');
            // regular cases
            for(int i = 0; i < waveformList.Length - 1; i++){
                waveformList[i] = waveformList[i].Trim('"');
            }
            // weird ASCII 10 value edge case for the last value in the array.
            string edgeCaseTemp = waveformList[waveformList.Length - 1];
            edgeCaseTemp = edgeCaseTemp.Substring(1, edgeCaseTemp.Length - 3); // handle that edge case
            waveformList[waveformList.Length - 1] = edgeCaseTemp;
            return waveformList;  // return the proper array
        }

        public override WaveformType GetWaveformType(int channel)
        {
            throw new NotImplementedException();
        }

        /*
         * Loads the waveform at the given internal USER memory location to the active memory for the given channel
         * Throws an ArgumentException if the given channel doesn't exist
         * Throws a FileNotFoundException if the given memory location doesn't exist. 
         */
        public override void LoadWaveform(string name, int channel)
        {
            CheckChannelParam(channel);
            if(name == "EMEM" || name == "emem")  // check if the client is trying to load edit memory into edit memory
            {
                return;  // EMEM is a valid memory location, so we don't wish to throw an exception, but edit memory
                // is already in edit memory, so we don't do anything.
            }
            if(!Array.Exists(GetWaveformList(), element => element == name))
            // if the requested file/memory location doesn't exist
            
            {
                throw new FileNotFoundException();  // throw a FileNotFoundException
            }
            WriteRawCommand("SOURce1:FUNCtion:SHAPe " + name);

        }

        public override void SetAmplitude(double amplitude, int channel)
        {
            throw new NotImplementedException();
        }

        public override void SetDCOffset(double dcOffset, int channel)
        {
            throw new NotImplementedException();
        }

        public override void SetFrequency(double frequency, int channel)
        {
            throw new NotImplementedException();
        }

        public override void SetHighLevel(double highLevel, int channel)
        {
            throw new NotImplementedException();
        }

        public override void SetLowLevel(double lowLevel, int channel)
        {
            throw new NotImplementedException();
        }

        public override void SetOutputOff(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("OUTP 0");
        }

        public override void SetOutputOn(int channel)
        {
            CheckChannelParam(channel);
            WriteRawCommand("OUTP 1");
        }

        public override void SetPhase(double phase, int channel)
        {
            throw new NotImplementedException();
        }

        public override void SetSampleRate(double sampleRate, int channel)
        {
            throw new NotImplementedException();
        }

        public override void SetWaveformType(WaveformType type, int channel)
        {
            throw new NotImplementedException();
        }

        public override void StoreAdditionalData(byte[] data, string memoryLocation)
        {
            throw new NotImplementedException();
        }

        // this function was removed from the interface, but is still left here for posterity.
        public void UploadCSVWaveform(FileStream file, string memoryLocation)
        {
            int count = 1;  // alas no zero based indexing with the waveform data either
            if(Path.GetExtension(file.Name) != ".csv")  // check to see if the given file is actually .csv
            {
                throw new ArgumentException();  // throw an ArgumentException if it is not
            }
            if (!GetValidMemoryLocations().Contains(memoryLocation))  // check to see if the requested memory location exists
            {
                throw new ArgumentException(); // throw an ArgumentException if it does not
            }
           
            Console.WriteLine("clearing edit memory");
            using (StreamReader streamReader = new StreamReader(file, Encoding.ASCII))
            {
                long numPoints = 0;
                string line;
                while ((line = streamReader.ReadLine()) != null) {  // get number of lines and therefore the number of points
                    numPoints++;
                }
                file.Position = 0;  // start over, reading the file from the beginning
                streamReader.DiscardBufferedData();
                WriteRawCommand("DATA:DEF EMEM," + numPoints);  // clear out the edit memory, set it to have as many points
                // as we need
                while ((line = streamReader.ReadLine()) != null)
                {
                    // parse the file, and for each point found, normalize it, and upload it to the function generator's edit
                    // memory.
                    string[] data = line.Split(',');
                    int amplitude;
                    Int32.TryParse(data[0], out amplitude);
                    amplitude += 8191;  // normalize the value for the function generator.
                    WriteRawCommand("DATA:VALue EMEM," + count + "," + amplitude);
                    // Console.WriteLine("Writing data value " + amplitude + " to point " + count);
                    count++;
                }
            }
        }

        public void UploadCSVWaveform(string filePath, string memoryLocation)
        {
            throw new NotImplementedException();
        }

        public override void UploadWaveformData(double[] voltageArray, double sampleRate, double DCOffset, double phase, string memoryLocation)
        {
            throw new NotImplementedException();
        }

        public override void UploadWaveformData(short[] waveData, double sampleRate, double lowLevel, double highLevel, double DCOffset, double phase, string memoryLocation)
        {
            throw new NotImplementedException();
        }
    }
}
