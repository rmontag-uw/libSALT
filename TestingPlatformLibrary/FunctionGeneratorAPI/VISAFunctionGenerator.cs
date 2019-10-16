using Ivi.Visa;
using NationalInstruments.Visa;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TestingPlatformLibrary.FunctionGeneratorAPI
{
    
    /// <summary>
    ///  This class provides a skeletal implementation of 
    ///  the IFunctionGenerator interface to minimize the effort required to implement the interface.
    ///  This implementation of the IFunctionGenerator interface uses (and requires) an IVI VISA compliant library in order to function.
    ///  This class covers some of the basic get methods, initialization, and the async read and write.
    ///  However, it is not required to extend this class to create a valid implementation of IFunctionGenerator.
    ///  Clients wishing to create a valid implementation of the IMassStorageFunctionGenerator interface can extend this class and then
    ///  implement that interface.
    /// </summary>
    public abstract class VISAFunctionGenerator : IFunctionGenerator
    {
        protected readonly string visaID;  // visaID of this function generator.
        protected readonly IResourceManager rm;  // the resource manager (only one instance per runtime)
        protected IMessageBasedSession mbSession;  // the message session between the computer and the function gen hardware
        protected int numChannels;  // the number of channels that this function generator has
        protected WaitHandle waitHandleIO;  // callback stuff
        protected readonly ManualResetEvent manualResetEventIO;
        private static readonly object threadLock = new object();  // for thread locking
        protected VISAFunctionGenerator(string visaID, IResourceManager rm, int numChannels)
        {
            this.visaID = visaID;  // set this visaID to the parameter visaID
            this.rm = rm;  // set the resource manager to the parameter rm.
            manualResetEventIO = new ManualResetEvent(false);  // init the manualResetEvent
            this.numChannels = numChannels;  // set the number of output channels that this function generator has
            mbSession = (IMessageBasedSession)rm.Open(this.visaID);  // open the message session 
            // between the computer and the function generator.
        }

        /*
         * Returns the number of output channels that this function generator has
         */
        /// <summary>
        /// Returns the number of seperate channels the function generator has.
        /// This could be set in the constructor.
        /// </summary>
        /// <returns>The number of seperate channels the function generator has</returns>
        public int GetNumChannels()
        {
            return numChannels;
        }

        /*
         * This function writes the given SCPI command to the function generator
         * If what you want to send doesn't end with a question mark, send it here.
         */
        /// <summary>
        /// Writes the SCPI command stored in the string to the function generator.
        /// </summary>
        /// <param name="command">The SCPI command to write</param>
        protected void WriteRawCommand(string command)
        {
            lock (threadLock)
            {
                mbSession.FormattedIO.WriteLine(command);
            }
        }

        protected void WriteRawData(byte[] data)
        {
            lock (threadLock)
            {
                bool completed;
                IVisaAsyncResult result = mbSession.RawIO.BeginWrite(data, new VisaAsyncCallback(OnWriteComplete), (object)data.Length);
                // get the Async result object from the operation
                waitHandleIO = result.AsyncWaitHandle;  // set the wait handle
                completed = waitHandleIO.WaitOne(30000);  // wait until the write operation has completed or timed out
                if (!(completed))  // check to see that the operation completed without timing out.
                {
                    throw new TimeoutException();  // if it did time out, throw a timeout exception
                }
                mbSession.RawIO.EndWrite(result);  // end the write, freeing up system resources.
            }
        }

        /*
         * This function writes the given SCPI query to the function generator, and returns the result.
         * If what you want to send ends with a question mark, send it here.
         */
        /// <summary>
        /// Writes the given query to the function generator, and returns the response as a string.
        /// </summary>
        /// <remarks>Most queries to the Function generator return a string, while commands return nothing, 
        /// therefore, they must
        /// be handled separately.It is up to the implementor to handle the two cases.</remarks>
        /// <param name="query">The query to write to the generator</param>
        /// <exception cref="Ivi.Visa.IOTimeoutException">Thrown if the query command is invalid</exception>
        /// <returns>The function generator's response to the given query</returns>
        protected string WriteRawQuery(string query)
        {
            lock (threadLock)
            {
                mbSession.FormattedIO.WriteLine(query);
                return mbSession.FormattedIO.ReadLine();
            }
        }

        /*
         * This function turns off the output for every channel on the function generator.
         */
        /// <summary>
        /// Turns off the output of the function generator for all channels.
        /// </summary>
        public void SetAllOutputsOff()
        {
            for(int i = 1; i <= numChannels; i++)  // not zero-based indexing.
            {
                SetOutputOff(i);  // just go through each of the channels the function generator has and switch them off.
            }
        }

        // HELPER METHODS

        /*
         * Remember there's no zero based indexing for the channels (unless an function generator actually has a channel
         * listed as channel zero but that's a bridge we'll burn when we get there)
         * 
         * This is just a little helper method to make the code that has to check for valid channels (and there's a lot of it)
         * look a little cleaner.
         */
        protected void CheckChannelParam(int channel)
        {
            if(channel > GetNumChannels() || channel < 1)  // throw an exception if the channel being asked for is
                // higher than what the function generator has, or if it's less than 1.
            {
                throw new ArgumentOutOfRangeException(
                    "The requested output channel does not exist on this function generator. " +
                    "The highest channel on this generator is: " + GetNumChannels() + ", and the channel requested was: " + channel);
            }
        }

        //helper methods

        private void OnWriteComplete(IVisaAsyncResult result)
        {
            manualResetEventIO.Set();  // set the IO manual reset event.

        }

        /// <summary>
        /// Returns an array of the user saved waveforms stored on the function generator.
        /// The returned values will likely be memory locations where the waveforms are stored, such as "USER2".
        /// </summary>
        /// <remarks> If a memory location is in the returned array, there is data stored there, uploading to it 
        /// will mean overwriting
        /// the waveform stored there. </remarks>
        /// <returns>An array of the user saved waveforms stored on the function generator</returns>
        public abstract string[] GetWaveformList();

        /// <summary>
        /// Returns an array of (the names of) valid internal user waveform memory locations on the function generator.
        /// This might need to be hard coded by implementors.
        /// </summary>
        /// <returns>An array of the names of valid internal user waveform memory locations on the function generator</returns>
        public abstract string[] GetValidMemoryLocations();

        /// <summary>
        /// Uploads the waveform data in the byte array with the given waveform parameters
        /// </summary>
        /// <param name="waveData">The waveform point data, in short array form</param>
        /// <param name="sampleRate">The sample rate for playback, in Sa/S</param>
        /// <param name="lowLevel">The lowest voltage in the wave, in V</param>
        /// <param name="highLevel">The highest voltage in the wave, in V</param>
        /// <param name="phase">The phase of the waveform to upload, in degrees</param>
        /// <param name="DCOffset">The DC offset for playback, in volts. 0 if none is needed</param>
        /// <param name="memoryLocation">The memory location to upload the waveform to in .bin file form</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any of the given values are out of range
        /// for the function generator, including if there are too many or not enough points</exception>
        public abstract void UploadWaveformData(short[] waveData, double sampleRate, double lowLevel, double highLevel, double DCOffset, double phase, string memoryLocation);

        /// <summary>
        /// Uploads the waveform data in the byte array with the given waveform parameters.
        /// </summary>
        /// <param name="voltageArray">The voltages that make up the waveform, each voltage is one point on the wave</param>
        /// <param name="sampleRate">The sample rate for playback, in Sa/S</param>
        /// <param name="DCOffset">The DC offset for playback, in Volts</param>
        /// <param name="phase">The phase of the waveform to upload, in degrees</param>
        /// <param name="memoryLocation">The memory location to upload the waveform to in .bin file form</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any of the given values are out of range
        /// for the function generator including if there are too many or not enough points</exception>
        public abstract void UploadWaveformData(double[] voltageArray, double sampleRate, double DCOffset, double phase, string memoryLocation);

        /// <summary>
        /// Loads the waveform in the given memory location from the function generator's internal memory into the
        /// active memory of the given channel and queues it. (i.e.switches the function generator to arbitrary mode)
        /// </summary>
        /// <remarks></remarks>
        /// <param name="name">The name or memory location of the waveform to load into active memory</param>
        /// <param name="channel">The channel to load to</param>
        /// <exception cref="System.IO.FileNotFoundException">Thrown when the requested memory location doesn't 
        /// exist on the function generator</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when the requested channel doesn't 
        /// exist on the function generator</exception>
        public abstract void LoadWaveform(string name, int channel);

        /// <summary>
        /// Generates a calibration waveform (500 Hz Sine wave), used for checking the test rig's oscillocope,
        /// and queues it on the given channel.
        /// </summary>
        /// <remarks>To actually play back the waveform, this function must be called, and 
        /// then <see cref="SetOutputOn(int)"/> to turn on the output</remarks>
        /// <param name="channel">The channel to generate the waveform on</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the requested channel 
        /// doesn't exist on the function generator</exception>
        public abstract void CalibrateWaveform(int channel);

        /// <summary>
        /// Sets the waveform frequency on the given channel to the given value
        /// </summary>
        /// <param name="frequency">The frequency to set to, in Hz</param>
        /// <param name="channel">the channel number to set the frequency of</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist 
        /// or if the frequency is out of range for the function generator</exception>
        public abstract void SetFrequency(double frequency, int channel);

        /// <summary>
        /// Returns the frequency in Hz that the given channel is playing its waveform back at
        /// </summary>
        /// <param name="channel">The channel number to get the frequency from</param>
        /// <returns>The frequency in Hz that the given channel is playing waveforms at</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        public abstract double GetFrequency(int channel);

        /// <summary>
        /// Sets the amplitude of the waveform of the given channel in Vpp.
        /// </summary>
        /// <param name="amplitude">The amplitude to set the channel to, in units of Vpp</param>
        /// <param name="channel">The channel to set the amplitude of</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist or 
        /// if the requested amplitude is not supported on the function generator</exception>
        public abstract void SetAmplitude(double amplitude, int channel);

        /// <summary>
        /// Returns the amplitude of the waveform on the given channel in Vpp.
        /// </summary>
        /// <param name="channel">the channel to get the amplitude from</param>
        /// <returns>The amplitude of the waveform on the given channel in Vpp</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        public abstract double GetAmplitude(int channel);

        /// <summary>
        /// Sets the DC offset for the given channel 
        /// </summary>
        /// <param name="DCOffset">The DC offset to set for the given channel, in Volts</param>
        /// <param name="channel">The channel to set the DC offset for</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        public abstract void SetDCOffset(double DCOffset, int channel);

        /// <summary>
        /// Gets the DC offset for the given channel
        /// </summary>
        /// <param name="channel">the channel to get the DC offset for</param>
        /// <returns>The DC offset (in Volts) for the given channel</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        public abstract double GetDCOffset(int channel);

        /// <summary>
        /// Sets the sample rate for the given channel
        /// </summary>
        /// <remarks>The generator must be in a point-by-point waveform generation mode 
        /// for this command to have an immediate effect</remarks>
        /// <param name="sampleRate">The sampling rate to set</param>
        /// <param name="channel">The channel to change the sampling rate for</param>
        public abstract void SetSampleRate(double sampleRate, int channel);

        /// <summary>
        /// Gets the current sampling rate for the given channel, returns -1 if the 
        /// generator is not set to a direct sample rate mode
        /// </summary>
        /// <param name="channel">The channel to get the sampling rate from</param>
        /// <returns>The current sampling rate for the given channel, -1 if generator not configured for sample rates</returns>
        public abstract double GetSampleRate(int channel);

        /// <summary>
        /// Sets the phase for the waveform on the given channel
        /// </summary>
        /// <param name="phase">The phase to set to, in degrees</param>
        /// <param name="channel">The channel to set the phase for</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel does not exist on the function generator</exception>
        public abstract void SetPhase(double phase, int channel);

        /// <summary>
        /// Gets the current phase for the waveform on the given channel, in degrees
        /// </summary>
        /// <param name="channel">The channel to get the phase from</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel does not exist on the function generator</exception>
        public abstract double GetPhase(int channel);

        /// <summary>
        /// Sets the high level voltage for the given channel
        /// </summary>
        /// <param name="highLevel">The high level to set, in volts</param>
        /// <param name="channel">The channel to set to</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel 
        ///  does not exist on the function generator or if the requested voltage is too high for the function generator</exception>
        public abstract void SetHighLevel(double highLevel, int channel);

        /// <summary>
        /// Gets the high level voltage for the given channel
        /// </summary>
        /// <param name="channel">The channel to get the high level voltage from</param>
        /// <returns>The high level voltage for the given channel</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel does not exist on the function generator</exception>
        public abstract double GetHighLevel(int channel);

        /// <summary>
        /// Sets the low level voltage for the given channel
        /// </summary>
        /// <param name="lowLevel">The low level voltage to set for the given channel</param>
        /// <param name="channel">The channel to set the low level voltage for</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel 
        ///  does not exist on the function generator or if the requested voltage is too low for the function generator</exception>
        public abstract void SetLowLevel(double lowLevel, int channel);

        /// <summary>
        /// Gets the low level voltage for the given channel
        /// </summary>
        /// <param name="channel">The channel to get the low level voltage from</param>
        /// <returns>The low level voltage for the given channel</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist on the function generator</exception>
        public abstract double GetLowLevel(int channel);

        /// <summary>
        /// Sets the waveform type of the given channel to the type passed in as a string
        /// </summary>
        /// <param name="type">The type of the waveform to set the given channel to</param>
        /// <param name="channel">The channel to set the waveform type of</param>
        /// <exception cref="ArgumentException">Thrown if the requested waveform type doesn't exist on the function generator</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        public abstract void SetWaveformType(WaveformType type, int channel);

        /// <summary>
        /// Gets the waveform type of the given channel
        /// </summary>
        /// <param name="channel">The channel to get the waveform type from</param>
        /// <returns>The waveform type of the given channel</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the requested channel doesn't exist</exception>
        public abstract WaveformType GetWaveformType(int channel);

        /// <summary>
        /// Stores additional information about the waveform in the given memory location. It is up to the implementor to decide where this
        /// data is actually stored.
        /// </summary>
        /// <param name="data">The additional data to store</param>
        /// <param name="memoryLocation">The memorylocation where the waveform to store additional info about is located</param>
        /// <exception cref="ArgumentException">Thrown if the given memory location is not valid for the function generator</exception>
        public abstract void StoreAdditionalData(byte[] data, string memoryLocation);

        /// <summary>
        /// Retrieves the additional information about the waveform in the given memory location. It is up to the client to parse the data.
        /// </summary>
        /// <param name="memoryLocation">The memory location of the waveform to retrieve additional information about</param>
        /// <returns>The data, stored as a byte array</returns>
        /// <exception cref="ArgumentException">Thrown if the given memory location is not valid for the function generator</exception>
        public abstract byte[] GetAdditionalData(string memoryLocation);

        /// <summary>
        /// Turns on the output of the function generator for the given channel.
        /// </summary>
        /// <remarks>This function does not use zero based indexing. channel 1 is channel 1.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the requested channel doesn't exist</exception>
        /// <param name="channel">The channel to set output to on</param>
        public abstract void SetOutputOn(int channel);

        /// <summary>
        /// Turns off the output of the function generator for the given channel.
        /// </summary>
        /// <remarks>This function does not use zero based indexing. channel 1 is channel 1.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the requested channel doesn't exist</exception>
        /// <param name="channel">The channel to set output to off</param>
        public abstract void SetOutputOff(int channel);
        public abstract double GetMaxSupportedVoltage();
        public abstract double GetMinSupportedVoltage();
        public abstract string GetModelString();
        public abstract ConnectionType GetConnectionType();

        public string GetIdentificationString()
        {
            string response = WriteRawQuery("*IDN?");
            string[] tokens = response.Split(',');
            string toReturn = tokens[0] + tokens[1] + tokens[2];
            return toReturn;
        }

        public DeviceType GetDeviceType()
        {
            return DeviceType.Function_Generator;
        }

        public struct ConnectedFunctionGeneratorStruct
        {
            public VISAFunctionGenerator[] connectedFunctionGenerators;
            public bool unknownFunctionGeneratorConnected;

            public ConnectedFunctionGeneratorStruct(VISAFunctionGenerator[] connectedFunctionGenerators, bool unknownFunctionGeneratorConnected)
            {
                this.connectedFunctionGenerators = connectedFunctionGenerators;
                this.unknownFunctionGeneratorConnected = unknownFunctionGeneratorConnected;
            }

        }
        public static ConnectedFunctionGeneratorStruct GetConnectedFunctionGenerators()
        {
            IEnumerable<string> resources;
            //IResourceManager rm = new ResourceManager();
            IMessageBasedSession searcherMBS;
            List<string> connectedDeviceModels = new List<string>();  // get a list of connected VISA device model names
            List<string> rawVISAIDs = new List<string>();  // get a list of connect VISA devices' returned IDs
            List<VISAFunctionGenerator> toReturn = new List<VISAFunctionGenerator>();
            bool unknownFunctionGeneratorFound = false;
            try
            {
                resources = GlobalResourceManager.Find("?*");  // find all connected VISA devices
                foreach (string s in resources)  // after this loop, connectedDeviceModels contains a list of connected devices in the form <Model>
                {
                    rawVISAIDs.Add(s);  // we need to add 
                    string IDNResponse;
                    searcherMBS = GlobalResourceManager.Open(s) as IMessageBasedSession;  // open the message session 

                    lock (threadLock)  // since we're doing stuff with I/O we need to use the lock
                    {
                        searcherMBS.FormattedIO.WriteLine("*IDN?");  // All SCPI compliant devices (and therefore all VISA devices) are required to respond
                                                                     // to the *IDN? query. 
                        IDNResponse = searcherMBS.FormattedIO.ReadLine();
                    }
                    string[] tokens = IDNResponse.Split(',');   // hopefully this isn't too slow
                    string formattedIDNString =  tokens[1];  // we run the IDN command on all connected devices
                                                                              // and then parse the response into the form <Model>
                    connectedDeviceModels.Add(formattedIDNString);
                }
                for (int i = 0; i < connectedDeviceModels.Count; i++)  // connectedDeviceModels.Count() == rawVISAIDs.Count()
                {
                    VISAFunctionGenerator temp = GetDeviceFromModelString(connectedDeviceModels[i], rawVISAIDs[i], rm);
                    if (temp == null)
                    {
                        unknownFunctionGeneratorFound = true;  // if there's one 
                    }
                    else
                    {
                        toReturn.Add(temp);
                    }
                }
                return new ConnectedFunctionGeneratorStruct(toReturn.ToArray(), unknownFunctionGeneratorFound);
            }
            catch (Ivi.Visa.NativeVisaException)  // if no devices are found, return a struct with an array of size 0
            {
                return new ConnectedFunctionGeneratorStruct(new VISAFunctionGenerator[0], false);
            }
        }

        // This should be replaced with some reflection based solution in the future, so that users would just need to drop in their scope implementation
        // and recompile without having to touch anything here.
        // if the modelString parameter is not found in the hardcoded list of valid function generators, this function returns null.
        // All the scopes returned by this function are initialized and ready to be written to/read from
        private static VISAFunctionGenerator GetDeviceFromModelString(string modelString, string VISAID, IResourceManager rm)
        {
            object[] parameterObject = { VISAID, rm };  // the parameters for all of the VISAFunctionGenerator subclass constructors
            // reflection time
            string currentNameSpace = typeof(VISAFunctionGenerator).Namespace; // get the namespace of this class, all subclasses must be
                                                                          // in the same namespace in order to be found and instantiated via reflection.
            IEnumerable<Type> types = typeof(VISAFunctionGenerator)  // get all (non abstract) subclasses of VISAFunctionGenerator that are located
                                                                // in the same namespace of VISAFunctionGenerator
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(VISAFunctionGenerator)) && !t.IsAbstract && t.Namespace == currentNameSpace);
            foreach (Type t in types)  // iterate over all found subclasses
            {
                if (modelString.Equals(t.Name))  // if the name of that subclass matches the model of the function generator found, bingo!
                {
                    return (VISAFunctionGenerator)Activator.CreateInstance(t, parameterObject);  // create an instance of the object with the proper
                                                                                            // constructor and return it
                }
            }
            // if there's no matches, then there's a FG connected that doesn't have an associated implementation, so we return null
            return null;
        }
    }
}
