using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SampleCaptura
{
    /// <summary>
    ///  Represents a DirectShow filter (e.g. video capture device, compression codec).
    /// </summary>
    public class Filter : IComparable
    {
        /// <summary> Human-readable name of the filter </summary>
        public string Name { get; }

        /// <summary> Unique string referencing this filter. This string can be used to recreate this filter. </summary>
        public string MonikerString { get; }

        /// <summary> Create a new filter from its moniker </summary>
        public Filter(IMoniker Moniker)
        {
            Name = GetName(Moniker);
            MonikerString = GetMonikerString(Moniker);
        }

        /// <summary> Retrieve the a moniker's display name (i.e. it's unique string) </summary>
        static string GetMonikerString(IMoniker Moniker)
        {
            Moniker.GetDisplayName(null, null, out var s);
            return s;
        }


        /// <summary>
		///  This method gets a UCOMIMoniker object.
		/// 
		///  HACK: The only way to create a UCOMIMoniker from a moniker 
		///  string is to use UCOMIMoniker.ParseDisplayName(). So I 
		///  need ANY UCOMIMoniker object so that I can call 
		///  ParseDisplayName(). Does anyone have a better solution?
		/// 
		///  This assumes there is at least one video compressor filter
		///  installed on the system.
		/// </summary>
		protected IMoniker getAnyMoniker()
        {
            Guid category = FilterCategory.VideoCompressorCategory;
            int hr;
            object comObj = null;
            ICreateDevEnum enumDev = null;
            IEnumMoniker enumMon = null;
            IMoniker[] mon = new IMoniker[1];

            try
            {
                // Get the system device enumerator
                Type srvType = Type.GetTypeFromCLSID(Clsid.SystemDeviceEnum);
                if (srvType == null)
                    throw new NotImplementedException("System Device Enumerator");
                comObj = Activator.CreateInstance(srvType);
                enumDev = (ICreateDevEnum)comObj;

                // Create an enumerator to find filters in category
                hr = enumDev.CreateClassEnumerator(category, out enumMon, 0);
                if (hr != 0)
                    throw new NotSupportedException("No devices of the category");

                // Get first filter
                IntPtr f = new IntPtr();
                hr = enumMon.Next(1, mon, f);
                if ((hr != 0))
                    mon[0] = null;

                return (mon[0]);
            }
            finally
            {
                enumDev = null;
                if (enumMon != null)
                    Marshal.ReleaseComObject(enumMon); enumMon = null;
                if (comObj != null)
                    Marshal.ReleaseComObject(comObj); comObj = null;
            }
        }

        public IMoniker CreateMoniker()
        {
            IMoniker parser = null;
            IMoniker moniker = null;

            try
            {
                parser = getAnyMoniker();
                int eaten;
                parser.ParseDisplayName(null, null, MonikerString, out eaten, out moniker);

                return moniker;
            }
            finally
            {
                if (parser != null)
                    Marshal.ReleaseComObject(parser); parser = null;
            }
        }

        /*
        /// <summary>
        /// Returns the video capabilities of the video device.
        /// </summary>
        /// <param name="filter">Specifies the video device.</param>
        /// <returns>A collection of video capabilities is returned for the device.</returns>
        public VideoCapabilityCollection GetVideoCapatiblities(Filter filter)
        {
            int hr;
            IFilterGraph2 grph = null;
            IBaseFilter camFltr = null;
            ICaptureGraphBuilder2 bldr = null;
            object comObj = null;
            AMMediaType mt = null;
            IntPtr pSC = IntPtr.Zero;
            VideoCapabilityCollection colCap = new VideoCapabilityCollection();

            try
            {
                if (filter == null)
                    return colCap;

                grph = (IFilterGraph2)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.FilterGraph, true));

                IMoniker moniker = filter.CreateMoniker();
                grph.AddSourceFilterForMoniker(moniker, null, filter.Name, out camFltr);
                Marshal.ReleaseComObject(moniker);

                bldr = (ICaptureGraphBuilder2)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.CaptureGraphBuilder2, true));
                hr = bldr.SetFiltergraph(grph as IGraphBuilder);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                // Add the web-cam filter to the graph.
                hr = grph.AddFilter(camFltr, filter.Name);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                // Get the IAMStreamConfig interface.
                Guid cat = PinCategory.Capture;
                Guid type = MediaType.Interleaved;
                Guid iid = typeof(IAMStreamConfig).GUID;

                hr = bldr.FindInterface(cat, type, camFltr, iid, out comObj);
                if (hr < 0)
                {
                    type = MediaType.Video;
                    hr = bldr.FindInterface(cat, type, camFltr, iid, out comObj);
                }

                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                IAMStreamConfig cfg = comObj as IAMStreamConfig;


                // Enumerate the video capabilities.
                int nCount;
                int nSize;
                hr = cfg.GetNumberOfCapabilities(out nCount, out nSize);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                VideoInfoHeader vih = new VideoInfoHeader();
                VideoStreamConfigCaps vsc = new VideoStreamConfigCaps();
                pSC = Marshal.AllocCoTaskMem(nSize);

                for (int i = 0; i < nCount; i++)
                {
                    AMMediaType pMT;
                    hr = cfg.GetStreamCaps(i, out pMT, pSC);
                    if (hr < 0)
                        Marshal.ThrowExceptionForHR(hr);

                    mt = Marshal.PtrToStructure<AMMediaType>(pMT);

                    Marshal.PtrToStructure(mt.formatPtr, vih);
                    Marshal.PtrToStructure(pSC, vsc);

                    int nWidth = vih.BmiHeader.Width;
                    int nHeight = vih.BmiHeader.Height;
                    int nFpsMin = (int)(10000000 / vsc.MaxFrameInterval);
                    int nFpsMax = (int)(10000000 / vsc.MinFrameInterval);

                    colCap.Add(new VideoCapability(nWidth, nHeight, nFpsMin, nFpsMax));

                    if (mt != null)
                    {
                        Marshal.FreeCoTaskMem(mt.formatPtr);
                        mt = null;
                    }
                }
            }
            catch (Exception excpt)
            {
                throw excpt;
            }
            finally
            {
                if (mt != null)
                    Marshal.FreeCoTaskMem(mt.formatPtr);

                if (comObj != null)
                    Marshal.ReleaseComObject(comObj);

                if (pSC != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pSC);

                if (bldr != null)
                    Marshal.ReleaseComObject(bldr);

                if (camFltr != null)
                    Marshal.ReleaseComObject(camFltr);

                if (grph != null)
                    Marshal.ReleaseComObject(grph);
            }

            return colCap;
        }
        */

        /// <summary> Retrieve the human-readable name of the filter </summary>
        static string GetName(IMoniker Moniker)
        {
            object bagObj = null;

            try
            {
                var bagId = typeof(IPropertyBag).GUID;
                Moniker.BindToStorage(null, null, ref bagId, out bagObj);
                var bag = (IPropertyBag)bagObj;
                var hr = bag.Read("FriendlyName", out var val, null);

                if (hr != 0)
                    Marshal.ThrowExceptionForHR(hr);

                var ret = val as string;

                if (string.IsNullOrEmpty(ret))
                    throw new NotImplementedException("Device FriendlyName");
                return ret;
            }
            catch (Exception)
            {
                return "";
            }
            finally
            {
                if (bagObj != null)
                    Marshal.ReleaseComObject(bagObj);
            }
        }

        /// <summary>
        ///  Compares the current instance with another object of the same type.
        /// </summary>
        public int CompareTo(object Obj)
        {
            if (Obj == null)
                return 1;

            var f = (Filter)Obj;

            return string.Compare(Name, f.Name, StringComparison.Ordinal);
        }



        public static IEnumerable<Filter> VideoInputDevices
        {
            get
            {
                object comObj = null;
                IEnumMoniker enumMon = null;
                var mon = new IMoniker[1];

                try
                {
                    // Get the system device enumerator
                    comObj = new CreateDevEnum();
                    var enumDev = (ICreateDevEnum)comObj;

                    var category = FilterCategory.VideoInputDevice;

                    // Create an enumerator to find filters in category
                    var hr = enumDev.CreateClassEnumerator(category, out enumMon, 0);
                    if (hr != 0)
                        yield break;

                    // Loop through the enumerator
                    do
                    {
                        // Next filter
                        hr = enumMon.Next(1, mon, IntPtr.Zero);

                        if (hr != 0 || mon[0] == null)
                            break;

                        // Add the filter
                        yield return new Filter(mon[0]);

                        // Release resources
                        Marshal.ReleaseComObject(mon[0]);
                        mon[0] = null;
                    } while (true);
                }
                finally
                {
                    if (mon[0] != null)
                        Marshal.ReleaseComObject(mon[0]);

                    mon[0] = null;

                    if (enumMon != null)
                        Marshal.ReleaseComObject(enumMon);

                    if (comObj != null)
                        Marshal.ReleaseComObject(comObj);
                }
            }
        }
    }
}
