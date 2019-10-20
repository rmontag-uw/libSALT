using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ivi.Visa;

namespace TestingPlatformLibrary
{
    /// <summary>
    /// This class provides abstracted low level VISA I/O functions.
    /// </summary>
    public abstract class VISADevice : ITestAndMeasurement
    {
        protected readonly string visaID;  // visaID of this oscilloscope.
        private readonly IMessageBasedSession mbSession;  // the message session between the computer and the oscilloscope hardware
                                                          // this used to be protected, if there are any extremely device specific reasons that require access to the message session, I might change it back
        private readonly object threadLock;  // each device can have its own I/O thread.
        private WaitHandle waitHandleIO;  // callback stuff
        private readonly ManualResetEvent manualResetEventIO;
        protected VISADevice(string visaID)
        {
            this.visaID = visaID;  // set this visaID to the parameter visaID
            threadLock = new object();  // each device needs its own locking object.
            manualResetEventIO = new ManualResetEvent(false);  // init the manualResetEvent
            mbSession = GlobalResourceManager.Open(this.visaID) as IMessageBasedSession;  // open the message session between the computer and the device.
        }

        protected void WriteRawCommand(string command)
        {
            lock (threadLock)
            {
                mbSession.FormattedIO.WriteLine(command);
            }
        }

        protected string WriteRawQuery(string query)
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

        protected void WriteRawData(byte[] data)
        {
            lock (threadLock)
            {
                bool completed;
                IVisaAsyncResult result = mbSession.RawIO.BeginWrite(data, new VisaAsyncCallback(OnWriteComplete), (object)data.Length);
                // get the Async result object from the operation
                waitHandleIO = result.AsyncWaitHandle;  // set the wait handle
                completed = waitHandleIO.WaitOne(30000);  // wait until the write operation has completed or timed out
                if (!completed)  // check to see that the operation completed without timing out.
                {
                    throw new TimeoutException();  // if it did time out, throw a timeout exception
                }
                mbSession.RawIO.EndWrite(result);  // end the write, freeing up system resources.
            }
        }

        /// <summary>
        /// I know that simply increasing the timeout before doing an operation and then setting it back down once complete might be bad style, 
        /// but it's what is required for some things.
        /// </summary>
        /// <remarks>This only sets the timeout value for the device that this function is called from (from its implementation)</remarks>
        /// <param name="time">The value to set I/O timeout to, in milliseconds</param>
        protected void SetIOTimeout(int time)  // should this be a public method?
        {
            mbSession.TimeoutMilliseconds = time;
        }

        /// <summary>
        /// This function returns an identification string which must be unique to the instrument. This string loosely follows the VISA
        /// *IDN? response format. The identification string must be in the format "<Manufacturer>, <Model>, <Serial_Number>"
        /// even if the device is not connected over a VISA interface. The device must be on and connected for this function to work.
        /// </summary>
        /// <returns>An identification string unique to the device, in the format "<Manufacturer>, <Model>, <Serial_Number>"</returns>
        /// <exception cref="InvalidOperationException">thrown if the device is not on or not connected</exception>
        public string GetIdentificationString()
        {
            string response;
            try
            {
                response = WriteRawQuery("*IDN?");
            }
            catch (IOTimeoutException e)  // if there's a VISA timeout
            {
                throw new InvalidOperationException("VISA Device Timeout", e);
            }
            string[] tokens = response.Split(',');
            string toReturn = tokens[0] + tokens[1] + tokens[2];
            return toReturn;
        }

        public abstract string GetModelString();
        public abstract ConnectionType GetConnectionType();
        public abstract DeviceType GetDeviceType();

        /// <summary>
        /// This class contains information about the VISA connected devices of a certain device type. Valid device types are the same as those in the ITestAndMeasurement DeviceTypeEnum.
        /// T must be a valid subclass of VISADevice. This class contains an array of all VISA connected devices of the same device type as T ready for I/O operations. 
        /// </summary>
        /// <remarks></remarks>
        /// <typeparam name="T">A subclass of VISADevice, the type of classes stored in the array of devices</typeparam>
        public struct ConnectedDeviceStruct<T> where T : VISADevice
        {
            public readonly T[] connectedDevices;  // an array of connected VISA devices, ready for I/O operations when needed.
            public readonly bool unknownDeviceConnected;
            /* if this flag is set to true, there is a device connected that there is no
             * associated T implementation for. 
             * TODO: This always is true when there are two different device types connected, my original plan was to have a way for
             * applications to tell the user that their scope didn't have an implementation, rather then just crashing or not connecting.
             * I need to find some way to fix that, for now just ignore this flag in application code.
             */

            public ConnectedDeviceStruct(T[] connectedDevices, bool unknownDeviceConnected)
            {
                this.connectedDevices = connectedDevices;
                this.unknownDeviceConnected = unknownDeviceConnected;
            }
        }

        /// <summary>
        /// Returns a ConnectedDevicestruct that contains information about the VISA devices of device type DeviceType.* connected to the system. 
        /// Only devices that can be located by a VISA
        /// library can be found by this function, e.g. something connected using raw sockets over LAN won't work. 
        /// NOT THREAD SAFE. This is best used before any regular I/O transfer has started in your program.
        /// </summary>
        /// <remarks>Look at ConnectedDeviceStruct documentation for additional info.</remarks>
        /// <returns>a ConnectedDeviceStruct, containing information about connected Devices of the passed in device type</returns>
        /// <exception cref="System.Runtime.InteropServices.ExternalException">Thrown if the program cannot locate a valid 
        /// VISA implementation on the system. There are no checked exceptions in C# but please,
        /// handle this one with a message in ANY application you build with this library.</exception>
        public static ConnectedDeviceStruct<T> GetConnectedDevices<T>() where T : VISADevice
        {
            IEnumerable<string> resources;
            IMessageBasedSession searcherMBS;
            List<string> connectedDeviceModels = new List<string>();  // get a list of connected VISA device model names
            List<string> rawVISAIDs = new List<string>();  // get a list of connect VISA devices' returned IDs
            List<T> toReturn = new List<T>();
            bool unknownDeviceFound = false;
            try
            {
                resources = GlobalResourceManager.Find("?*");  // find all connected VISA devices
                foreach (string s in resources)  // after this loop, connectedDeviceModels contains a list of connected devices in the form <Model>
                {
                    rawVISAIDs.Add(s);  // we need to add 
                    string IDNResponse;
                    searcherMBS = GlobalResourceManager.Open(s) as IMessageBasedSession;  // open the message session 
                    searcherMBS.FormattedIO.WriteLine("*IDN?");  // All SCPI compliant devices (and therefore all VISA devices) are required to respon to *IDN?
                    IDNResponse = searcherMBS.FormattedIO.ReadLine();
                    string[] tokens = IDNResponse.Split(',');   // hopefully this isn't too slow
                    string formattedIDNString = tokens[1];  // we run the IDN command on all connected devices
                                                            // and then parse the response into the form <Model>
                    connectedDeviceModels.Add(formattedIDNString);
                }
                for (int i = 0; i < connectedDeviceModels.Count; i++)  // connectedDeviceModels.Count() == rawVISAIDs.Count()
                {
                    T temp = GetDeviceFromModelString<T>(connectedDeviceModels[i], rawVISAIDs[i]);
                    if (temp == null)
                    {
                        unknownDeviceFound = true;  // if there's one 
                    }
                    else
                    {
                        toReturn.Add(temp);
                    }
                }
                return new ConnectedDeviceStruct<T>(toReturn.ToArray(), unknownDeviceFound);
            }
            catch (VisaException)  // if no devices are found, return a struct with an array of size 0
            {
                return new ConnectedDeviceStruct<T>(new T[0], false);
            }
        }

        private static T GetDeviceFromModelString<T>(string modelString, string VISAID) where T : VISADevice

            // returns a instantiated VISADevice object created from its "VISA ID" and
            // its modelString
        {
            object[] parameterObjects = { VISAID };  // the parameters for all of the VISADevice subclass constructors
            // reflection time
            string currentNameSpace = typeof(T).Namespace; // get the namespace of this class, all subclasses must be
                                                           // in the same namespace in order to be found and instantiated via reflection.
            IEnumerable<Type> types = typeof(T)  // get all (non abstract) subclasses of T that are located
                                                 // in the same namespace of T
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(T)) && !t.IsAbstract && t.Namespace == currentNameSpace);
            foreach (Type t in types)  // iterate over all found subclasses
            {
                if (modelString.Equals(t.Name))  // if the name of that subclass matches the model of the device found, bingo!
                {
                    return (T)Activator.CreateInstance(t, parameterObjects);  // create an instance of the object with the proper
                                                                              // constructor and return it
                }
            }
            // if there's no matches, then there's a device connected that doesn't have an associated implementation, so we return null
            return null;
        }
        private void OnWriteComplete(IVisaAsyncResult result)  // for callbacks on data writing
        {
            manualResetEventIO.Set();  // set the IO manual reset event.

        }

        /// <summary>
        /// Per SCPI (and now VISA) standards, all VISA devices must adhere to the 10-ish IEEE 488.2 standard commands.
        /// The command in question here is *RST
        /// <remarks>Use this one with caution, as the instrument could possible end up in an unknown(to the program) state</remarks>
        /// </summary>
        public void Reset()
        {
            WriteRawCommand("*RST");
        }
    }
}