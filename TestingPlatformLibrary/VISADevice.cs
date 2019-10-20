using System;
using System.Collections.Generic;
using System.Linq;
using Ivi.Visa;

namespace TestingPlatformLibrary
{
    /// <summary>
    /// This class provides abstracted low level VISA I/O functions.
    /// </summary>
    public abstract class VISADevice : ITestAndMeasurement
    {
        protected readonly string visaID;  // visaID of this oscilloscope.
        protected static readonly Ivi.Visa.Interop.IResourceManager rm = new ResourceManagerClass();  // might only need one of these per runtime
        protected readonly IFormattedIO488 IOSession;
        private readonly object threadLock;  // each device can have its own I/O thread.
        protected VISADevice(string visaID)
        {
            this.visaID = visaID;  // set this visaID to the parameter visaID
                                   // rm = new ResourceManagerClass();
            threadLock = new object();
            IOSession = new FormattedIO488Class
            {
                IO = rm.Open(this.visaID) as IMessage  // IOSession.IO
            };
        }

        /// <summary>
        /// Write a raw SCPI command to the device.
        /// </summary>
        /// <param name="command"></param>
        protected void WriteRawCommand(string command)
        {

            //lock (threadLock)
            //{
            // Console.WriteLine("locked on writeRawCom from " + visaID);
            IOSession.WriteString(command);
            //}
            // Console.WriteLine("unlocked on writeRawCom " + visaID);
        }

        /// <summary>
        /// Write a raw SCPI query to the device, and return the response as a string. If it ends with a question mark, send it here
        /// </summary>
        /// <remarks>it is up to the implementor to handle the two cases for command vs query</remarks>
        /// <param name="query">The query to the device</param>
        /// <exception cref="Ivi.Visa.IOTimeoutException">Thrown if the query command is invalid</exception>
        /// <returns>The device's response as a string</returns>
        protected string WriteRawQuery(string query)
        {
            lock (threadLock)
            {
            IOSession.WriteString(query);
            }
            return IOSession.ReadString();
        }

        /// <summary>
        /// Reads 1024 raw bytes binary data from the device, in response to a query.
        /// </summary>
        /// <param name="query">The query for the device to respond to</param>
        /// <returns>The device's response, in byte[] form</returns>
        protected byte[] ReadRawData(string query)
        {
            return ReadRawData(query, 1024); // if bytesToRead isn't specified, just use 1024 by default. 
        }

        /// <summary>
        /// Reads bytesToRead bytes of raw binary data from the device in response to a query
        /// </summary>
        /// <param name="query">The query for the device to respond to</param>
        /// <param name="bytesToRead">The number of bytes to read</param>
        /// <returns>The device's response in byte[] form</returns>
        protected byte[] ReadRawData(string query, int bytesToRead)
        {
            lock (threadLock)
            {
            IOSession.WriteString(query);
            return IOSession.IO.Read(bytesToRead);
            }
        }

        /// <summary>
        /// Writes a byte array of raw data to the device.
        /// </summary>
        /// <param name="data">The byte[] of data to write to the device</param>
        protected void WriteRawData(byte[] data)
        {
            lock (threadLock) { 
            IOSession.IO.Write(data, data.Length);
            }
        }

        /// <summary>
        /// I know that simply increasing the timeout before doing an operation and then setting it back down once complete might be bad style, but it's what is required for some things.
        /// </summary>
        /// <param name="time">The time to set the I/O timeout value to</param>
        protected void SetIOTimeout(int time)
        {
            IOSession.IO.Timeout = time;
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
        /// </summary>
        /// <param name="deviceType">The ITestAndMeasurement device type to search for on the system</param>
        /// <remarks>Look at ConnectedDeviceStruct documentation for additional info.</remarks>
        /// <returns>a ConnectedDeviceStruct, containing information about connected Devices of the passed in device type</returns>
        /// <exception cref="System.Runtime.InteropServices.ExternalException">Thrown if the program cannot locate a valid 
        /// VISA implementation on the system. There are no checked exceptions in C# but please,
        /// handle this one with a message in ANY application you build with this library.</exception>
        protected static ConnectedDeviceStruct<T> GetConnectectedDevices<T>() where T : VISADevice
        {
            IEnumerable<string> resources;
            Ivi.Visa.Interop.IResourceManager searcherRM = new ResourceManagerClass();   // COM is absolutely dreadful
            Ivi.Visa.Interop.FormattedIO488 searcherMBS = new FormattedIO488Class();  // I'm going to have to write my own .NET wrapper for the COM visa
            // because the way this works is just horrible compared to the pure .NET version. 
            List<string> connectedDeviceModels = new List<string>();  // a list of the model names of all connected oscilloscopes
            List<string> rawVISAIDs = new List<string>();  // a list of all the raw VISA ids (what i'm calling the responses from .Find())
            List<T> toReturn = new List<T>();
            bool unknownDevicesFound = false;
            try
            {
                resources = searcherRM.FindRsrc("?*");  // find all connected VISA devices
                foreach (string s in resources)  // after this loop, connectedDeviceModels contains a list of connected devices in the form <Manufacturer>, <Model>
                {
                    rawVISAIDs.Add(s);  // we need to add 
                    string IDNResponse;
                    try
                    {
                        searcherMBS.IO = searcherRM.Open(s) as IMessage;
                    }
                    catch (TypeInitializationException ex)
                    {
                        if (ex.InnerException != null && ex.InnerException is DllNotFoundException)  // missing VISA implementation DLL
                        {
                            // how will we signal to applications that there's a library missing while still keeping abstraction and other stuff
                            // we'll let the client deal with it by throwing an ExternalException. 
                            throw new System.Runtime.InteropServices.ExternalException("Compatible VISA Library not found on System", ex.InnerException);

                            // if it's something else then we need to just throw it again
                        }
                        throw ex;
                    }

                    lock (s)  // since we're doing stuff with I/O we need to use the lock. Since strings in c# are likely aliased, this might give us a little bit of leeway
                              // if this function was called while an application was still performing I/O operations with a device.
                              // please don't do that if you don't have to okay.
                    {
                        searcherMBS.IO.WriteString("*IDN?");  // All SCPI compliant devices (and therefore all VISA devices) are required to respond
                                                              // to the *IDN? query. 
                        IDNResponse = searcherMBS.ReadString();
                    }
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
                        unknownDevicesFound = true;  // if there's a VISA device found that doesn't match any of our scope implementations
                                                     // sadly for us, function generators and other VISA devices are located, and then flagged as unknown. I'll look into if fixing this is even possible.
                                                     // it's only purpose is to help users debug. It might not be obvious that each scope needs its own implementation, and just having nothing show up could be a sign
                                                     // of a protocol or VISA error, and providing distinction would be nice.
                    }
                    else
                    {
                        toReturn.Add(temp);
                    }
                }
                return new ConnectedDeviceStruct<T>(toReturn.ToArray(), unknownDevicesFound);
            }
            catch (Exception ex) when (ex is VisaException || ex is System.Runtime.InteropServices.COMException)  // if no devices are found, return a struct with an array of size 0
            {
                return new ConnectedDeviceStruct<T>(new T[0], false);
            }
        }

        private static T GetDeviceFromModelString<T>(string modelString, string VISAID) where T : VISADevice

            // returns a instantiated VISADevice object created from its "VISA ID" and
            // its modelString
        {
            object[] parameterObjects = { VISAID };  // the parameters for all of the VISAOscilloscope subclass constructors
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
    }
}
