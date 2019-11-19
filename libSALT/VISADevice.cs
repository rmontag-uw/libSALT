using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ivi.Visa;

namespace libSALT
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
                try  // are these try/catch blocks too slow???
                {
                    mbSession.FormattedIO.WriteLine(query);
                    return mbSession.FormattedIO.ReadLine();
                }
                catch (IOTimeoutException e)
                {
                    throw new TimeoutException("Timeout in WriteRawQuery", e);
                }
            }
        }

        protected virtual byte[] ReadRawData(string query)
        {
            lock (threadLock)
            {
                try
                {
                    mbSession.FormattedIO.WriteLine(query);
                    return mbSession.RawIO.Read();
                }
                catch (IOTimeoutException e)
                {
                    throw new TimeoutException("Timeout in ReadRawData", e);
                }
            }
        }

        protected virtual byte[] ReadRawData(string query, int bytesToRead)
        {
            lock (threadLock)
            {
                try
                {
                    mbSession.FormattedIO.WriteLine(query);
                    return mbSession.RawIO.Read(bytesToRead);
                }
                catch (IOTimeoutException e)
                {
                    throw new TimeoutException("Timeout in ReadRawData", e);
                }
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
                    throw new TimeoutException("Timeout in WriteRawData");  // if it did time out, throw a timeout exception
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
            if (time >= 0)
            {
                mbSession.TimeoutMilliseconds = time;
            } else
            {
                mbSession.TimeoutMilliseconds = VisaConstants.InfiniteTimeout;  // this should work
            }
        }

        /// <summary>
        /// This function returns an identification string which must be unique to the instrument. This string loosely follows the VISA
        /// *IDN? response format. The identification string must be in the format "<Manufacturer>, <Model>, <Serial_Number>"
        /// even if the device is not connected over a VISA interface. The device must be on and connected for this function to work.
        /// </summary>
        /// <returns>An identification string unique to the device, in the format "<Manufacturer>, <Model>, <Serial_Number>"</returns>
        /// <exception cref="TimeoutException">thrown if the device is not on or not connected</exception>
        public string GetIdentificationString()
        {
            string response;
            try
            {
                response = WriteRawQuery("*IDN?");
            }
            catch (IOTimeoutException e)  // if there's a VISA timeout (we should catch VISA exceptions and then throw non-VISA ones)
            {
                throw new TimeoutException("VISA Device Timeout", e);
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
        public static ConnectedDeviceStruct<T> GetConnectedDevices<T>() where T : VISADevice, ITestAndMeasurement
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

        private static T GetDeviceFromModelString<T>(string modelString, string VISAID) where T : VISADevice, ITestAndMeasurement

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

        // attempts to open a VISA resource given the VISA identifier, returns null if the resource does not exist or cannot be opened.
        protected static T TryOpen<T>(string visaID) where T : VISADevice, ITestAndMeasurement
        {
            try
            {
                IMessageBasedSession searcherMBS = GlobalResourceManager.Open(visaID) as IMessageBasedSession;  // attempt to open the message session
                searcherMBS.FormattedIO.WriteLine("*IDN?");  // All SCPI compliant devices (and therefore all VISA devices) are required to respon to *IDN?
                string IDNResponse = searcherMBS.FormattedIO.ReadLine();
                string[] tokens = IDNResponse.Split(',');   // hopefully this isn't too slow
                string formattedIDNString = tokens[1];  // we run the IDN command on all connected devices
                T temp = GetDeviceFromModelString<T>(formattedIDNString, visaID);
                return temp;
            }
            catch (VisaException)
            {
                return null;  // if there is an error in the constructor, return null.
                // Yes I know exception handling is expensive and should not be used for control flow purposes, but the VISA functions throw an error instead of returning
                // null or something when a device cannot be opened.
            }
        }


        private void OnWriteComplete(IVisaAsyncResult result)  // for callbacks on data writing
        {
            manualResetEventIO.Set();  // set the IO manual reset event.

        }

        /// <summary>
        /// Writes the specified command to the device, using the service request event to wait until completion
        /// </summary>
        /// <param name="command">The command to write</param>
        /// <param name="timeout">The timeout of the command, in ms</param>
        /// <remarks>Currently unused, but could be extremely useful in the future</remarks>
        /// <exception cref="NativeVisaException">Thrown if the request times out</exception>
        private void WriteRawCommandWithServiceRequest(string command, int timeout)
        {
            lock (threadLock)  // need to use the actual mbSession functions instead of the VISADevice ones because this command needs its own lock
            {
                mbSession.FormattedIO.WriteLine("*ESR?");   // clear out the event status register
                mbSession.FormattedIO.ReadLine();
                mbSession.DiscardEvents(EventType.ServiceRequest);  // clear event buffer just in case
                mbSession.EnableEvent(EventType.ServiceRequest);
                mbSession.FormattedIO.WriteLine(command + ";*OPC");
                mbSession.WaitOnEvent(EventType.ServiceRequest, timeout);
                mbSession.FormattedIO.WriteLine("*ESR?");   // clear out the event status register again just in case
                mbSession.FormattedIO.ReadLine();
            }
        }

        /// <summary>
        /// Per SCPI (and now VISA) standards, all VISA devices must adhere to the 10-ish IEEE 488.2 standard commands.
        /// The command in question here is *RST
        /// <remarks>Use this one with caution, as the instrument could possible end up in an unknown(to the program) state</remarks>
        /// </summary>
        public void Reset()
        {
            //lock (threadLock)
            //{
            //    mbSession.FormattedIO.WriteLine("RST*");
            //    Thread.Sleep(5000);  // there is actually no other way to do this. All event/status byte registers are cleared when doing a reset command
            //                         // which means that trying to do this with a callback will always result in a timeout error. 5 seconds seems like 
            //                         //enough to me.
            //}
        }
    }
}