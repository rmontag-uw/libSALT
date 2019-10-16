using NationalInstruments.Visa;
using Ivi.Visa;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace TestingPlatformLibrary.OscilloscopeAPI
{
    public abstract class VISAOscilloscope : IOscilloscope
    {
        protected readonly string visaID;  // visaID of this oscilloscope.
        protected readonly IResourceManager rm;  // the resource manager (only one instance per runtime)
        protected IMessageBasedSession mbSession;  // the message session between the computer and the oscilloscope hardware
        protected int numChannels;  // the number of channels that this oscilloscope has
        protected WaitHandle waitHandleIO;
        protected readonly ManualResetEvent manualResetEventIO;
        protected static readonly object threadLock = new object();

        protected VISAOscilloscope(string visaID, IResourceManager rm, int numChannels)
        {
            this.visaID = visaID;  // set this visaID to the parameter visaID
            this.rm = rm;  // set the resource manager to the parameter rm.
            manualResetEventIO = new ManualResetEvent(false);  // init the manualResetEvent
            this.numChannels = numChannels;  // set the number of output channels that this function generator has
            mbSession = (IMessageBasedSession)rm.Open(this.visaID);  // open the message session 

            // between the computer and the function generator.
        }

        public int GetNumChannels()
        {
            return numChannels;
        }

        public void WriteRawCommand(string command)
        {
            lock (threadLock)
            {
                mbSession.FormattedIO.WriteLine(command);
            }
        }

        public string WriteRawQuery(string query)
        {
            lock (threadLock)
            {
                mbSession.FormattedIO.WriteLine(query);
                return mbSession.FormattedIO.ReadLine();
            }
        }

        protected virtual byte[] ReadRawData(string query)
        {
            lock (threadLock)
            {
                mbSession.FormattedIO.WriteLine(query);
                return mbSession.RawIO.Read();
            }
        }

        protected virtual byte[] ReadRawData(string query, int bytesToRead)
        {
            lock (threadLock)
            {
                mbSession.FormattedIO.WriteLine(query);
                return mbSession.RawIO.Read(bytesToRead);
            }
        }

        protected void CheckChannelParam(int channel)
        {
            if (channel > GetNumChannels() || channel < 1)  // throw an exception if the channel being asked for is
                                                            // higher than what the oscilloscope has, or if it's less than 1.
            {
                throw new ArgumentOutOfRangeException(
                    "The requested output channel does not exist on this oscilloscope. " +
                    "The highest channel on this scope is: " + GetNumChannels() + ", and the channel requested was: " + channel);
            }
        }

        public string GetIdentificationString()
        {
            string response = WriteRawQuery("*IDN?");
            string[] tokens = response.Split(',');
            string toReturn = tokens[0] + tokens[1] + tokens[2];
            return toReturn;
        }

        public DeviceType GetDeviceType()
        {
            return DeviceType.Oscilloscope;
        }

        public struct ConnectedOscilloscopeStruct
        {
            public VISAOscilloscope[] connectedOscilloscopes;
            public bool unknownOscilloscopeConnected;

            public ConnectedOscilloscopeStruct(VISAOscilloscope[] connectedOscilloscopes, bool unknownOscilloscopeConnected)
            {
                this.connectedOscilloscopes = connectedOscilloscopes;
                this.unknownOscilloscopeConnected = unknownOscilloscopeConnected;
            }

        }

        public static ConnectedOscilloscopeStruct GetConnectedOscilloscopes()
        {
            IEnumerable<string> resources;
            IResourceManager rm = new ResourceManager();
            IMessageBasedSession searcherMBS;
            List<string> connectedDeviceModels = new List<string>();  // a list of the model names of all connected oscilloscopes
            List<string> rawVISAIDs = new List<string>();  // a list of all the raw VISA ids (what i'm calling the responses from .Find())
            List<VISAOscilloscope> toReturn = new List<VISAOscilloscope>();
            bool unknownOscilloscopesFound = false;
            try
            {
                resources = rm.Find("?*");  // find all connected VISA devices
                foreach (string s in resources)  // after this loop, connectedDeviceModels contains a list of connected devices in the form <Manufacturer>, <Model>
                {
                    rawVISAIDs.Add(s);  // we need to add 
                    string IDNResponse;
                    searcherMBS = (IMessageBasedSession)rm.Open(s);  // open the message session 

                    lock (threadLock)  // since we're doing stuff with I/O we need to use the lock
                    {
                        searcherMBS.FormattedIO.WriteLine("*IDN?");  // All SCPI compliant devices (and therefore all VISA devices) are required to respond
                                                                     // to the *IDN? query. 
                        IDNResponse = searcherMBS.FormattedIO.ReadLine();
                    }
                    string[] tokens = IDNResponse.Split(',');   // hopefully this isn't too slow
                    string formattedIDNString = tokens[1];  // we run the IDN command on all connected devices
                                                                              // and then parse the response into the form <Model>
                    connectedDeviceModels.Add(formattedIDNString);
                }
                for (int i = 0; i < connectedDeviceModels.Count; i++)  // connectedDeviceModels.Count() == rawVISAIDs.Count()
                {
                    VISAOscilloscope temp = GetDeviceFromModelString(connectedDeviceModels[i], rawVISAIDs[i], rm);
                    if (temp == null)
                    {
                        unknownOscilloscopesFound = true;  // if there's a VISA device found that matches 
                    }
                    else
                    {
                        toReturn.Add(temp);
                    }
                }
                return new ConnectedOscilloscopeStruct(toReturn.ToArray(), unknownOscilloscopesFound);
            }
            catch (Ivi.Visa.NativeVisaException)  // if no devices are found, return a struct with an array of size 0
            {
                return new ConnectedOscilloscopeStruct(new VISAOscilloscope[0], false);
            }
        }

        private static VISAOscilloscope GetDeviceFromModelString(string modelString, string VISAID, IResourceManager rm)
        {
            object[] parameterObject = { VISAID, rm };  // the parameters for all of the VISAOscilloscope subclass constructors
            // reflection time
            string currentNameSpace = typeof(VISAOscilloscope).Namespace; // get the namespace of this class, all subclasses must be
                // in the same namespace in order to be found and instantiated via reflection.
            IEnumerable<Type> types = typeof(VISAOscilloscope)  // get all (non abstract) subclasses of VISAOscilloscope that are located
                    // in the same namespace of VISAOscilloscope
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(VISAOscilloscope)) && !t.IsAbstract && t.Namespace == currentNameSpace);
            foreach (Type t in types)  // iterate over all found subclasses
            {
                if (modelString.Equals(t.Name))  // if the name of that subclass matches the model of the oscilloscope found, bingo!
                {
                    return (VISAOscilloscope) Activator.CreateInstance(t, parameterObject);  // create an instance of the object with the proper
                        // constructor and return it
                }
            }
            // if there's no matches, then there's a scope connected that doesn't have an associated implementation, so we return null
            return null;
        }

        public abstract byte[] GetWaveData(int channel);
        public abstract double[] GetWaveVoltages(int channel);
        public abstract void Run();
        public abstract void Stop();
        public abstract void EnableChannel(int channel);
        public abstract void DisableChannel(int channel);
        public abstract bool IsChannelEnabled(int channel);
        public abstract void AutoScale();
        public abstract void SetActiveChannel(int channel);
        public abstract int GetActiveChannel();
        public abstract double GetMinimumVoltageScale();
        public abstract double GetYAxisOffsetScaleConstant();
        public abstract double GetTriggerPositionScaleConstant();
        public abstract double GetXAxisOffsetScaleConstant();
        public abstract double[] GetVoltageScalePresets();
        public abstract string[] GetVoltageScalePresetStrings();
        public abstract double[] GetTimeScalePresets();
        public abstract string[] GetTimeScalePresetStrings();
        public abstract double GetMinimumTimeScale();
        public abstract double GetMaximumVoltageScale();
        public abstract double GetMaximumTimeScale();
        public abstract double GetYScale(int channel);
        public abstract double GetYScale();
        public abstract void SetYScale(int channel, double voltageScale);
        public abstract double GetYIncrement(int channel);
        public abstract double GetXIncrement(int channel);
        public abstract double GetXIncrement();
        public abstract double GetYIncrement();
        //public abstract double GetYOrigin(int channel);
        //public abstract double GetYOrigin();
        //public abstract double GetYReference(int channel);
        //public abstract double GetYReference();
        public abstract double GetXAxisScale();
        public abstract void SetXAxisScale(double timeScale);
        public abstract double GetYAxisOffset(int channel);
        public abstract void SetYAxisOffset(int channel, double offset);
        public abstract double GetXAxisOffset();
        public abstract void SetXAxisOffset(double offset);
        public abstract double GetTriggerLevel();
        public abstract void SetTriggerLevel(double voltageLevel);
        public abstract int GetMemDepth();
        public abstract void SetMemDepth(int points);
        public abstract int[] GetAllowedMemDepths();
        public abstract void Single();
        public abstract byte[] GetDeepMemData(int channel);
        public abstract double[] GetDeepMemVoltages(int channel);
        public abstract Color GetChannelColor(int channel);
        public abstract string GetModelString();
        public abstract ConnectionType GetConnectionType();
    }
}
