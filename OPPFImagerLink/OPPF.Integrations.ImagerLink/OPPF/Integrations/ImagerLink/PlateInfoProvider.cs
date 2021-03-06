using System;
using System.Net;

using OPPF.Proxies;
using OPPF.Utilities;
using Formulatrix.Integrations.ImagerLink;
using log4net;
using log4net.Config;

namespace OPPF.Integrations.ImagerLink
{
    /// <summary>
    /// Provides plate information.
    /// </summary>
    /// <remarks>Both RockImager and RockImagerProcessor instantiate and use
    /// IPlateInfoProvider. The function of the class is to provide information
    /// about a plate for the user.
    /// Khalid's comments:
    /// The methods of IPlateInfoProvider are required to display information to the
    /// user about the plate. You should provide as much detail of the plate as
    /// possible. If you don't have a field, let me know and we may be able to
    /// figure out a value you can substitute for the user instead: for example, our
    /// other customer simply returns the barcode for GetPlateID().
    /// </remarks>
    public class PlateInfoProvider : IPlateInfoProvider
    {

        /// <summary>
        /// 15 min cache lifetime 
        /// </summary>
        private static readonly long _cacheLifetime = 15 * TimeSpan.TicksPerMinute;

        /// <summary>
        /// Cached list of plate types
        /// </summary>
        private static IPlateType[] _plateTypes = null;

        /// <summary>
        /// Expiry date of the cache in ticks
        /// </summary>
        private static long _plateTypesCacheExpires = 0;

        /// <summary>
        /// Lock object for plate types cache
        /// </summary>
        private static System.Object _plateTypesLock = new System.Object();

        /// <summary>
        ///  Logger
        /// </summary>
        private readonly ILog _log;

        /// <summary>
        /// PlateInfo cache
        /// </summary>
        private readonly PlateInfoCache _plateInfoCache;

        /// <summary>
        /// Zero-arg constructor
        /// </summary>
        public PlateInfoProvider()
        {

            // Load configuration
            OPPFConfigXML.Configure();

            // Get Logger
            _log = LogManager.GetLogger(this.GetType());

            // PlateInfoCache
            // TODO: Allow configuration of initialSize and Capacity
            _plateInfoCache = new PlateInfoCache(1000, 1000);

            // Log the call to the constructor
            if (_log.IsDebugEnabled)
            {
                string msg = "Constructed a new " + this;
                _log.Debug(msg);
            }

        }

        #region IPlateInfoProvider Members

        /// <summary>
        /// Retrieves a plate id.
        /// </summary>
        /// <param name="robot">The robot to find the plate type for.</param>
        /// <param name="barcode">The <c>barcode</c> label of the plate.</param>
        /// <returns>The unique identifier describing the plate. Actually, the barcode is the unique identifier, so it just returns the barcode</returns>
        public string GetPlateID(IRobot robot, string barcode)
        {

            // OPPF PERFORMANCE BODGE - The barcode is the plateID
            return barcode;

        }

        /// <summary>
        /// Retrieves a plate description.
        /// </summary>
        /// <param name="robot">The robot to find the plate type for.</param>
        /// <param name="plateID">The <c>plateID</c> of the plate.</param>
        /// <returns>The <c>IPlateInfo</c> describing the plate.</returns>
        public IPlateInfo GetPlateInfo(IRobot robot, string plateID)
        {
            return _plateInfoCache.GetPlateInfo(robot, plateID);

            /*
             * Replaced by cache
             * 
            // Check arguments - do it up front to avoid possible inconsistencies later
            if (robot == null) throw new System.NullReferenceException("robot must not be null");
            if (plateID == null) throw new System.NullReferenceException("plateID must not be null");

            // Log the call
            if (_log.IsDebugEnabled)
            {
                string msg = "Called " + this + ".GetPlateInfo(robot=" + robot.ToString() + ", plateID=\"" + plateID + "\")";
                _log.Debug(msg);
            }

            // Special case for ReliabilityTestPlate
            if ("ReliabilityTestPlate".Equals(plateID))
            {
                OPPF.Integrations.ImagerLink.PlateInfo dummy = new OPPF.Integrations.ImagerLink.PlateInfo();
                dummy.DateDispensed = DateTime.Now;
                dummy.ExperimentName = "Dummy Expt Name";
                dummy.PlateNumber = 1;
                dummy.PlateTypeID = "1";
                dummy.ProjectName = "Dummy Project Name";
                dummy.UserEmail = "DummyEmailAddress";
                dummy.UserName = "Dummy UserName";

                return dummy;
            }

            // Declare the return variable
            OPPF.Integrations.ImagerLink.PlateInfo pi = null;

            try
            {
                // Create and populate the request object
                getPlateInfo request = new getPlateInfo();
                request.robot = OPPF.Utilities.RobotUtils.createProxy(robot);
                request.plateID = plateID;

                // Make the web service call
                WSPlate wsPlate = new WSPlate();
                getPlateInfoResponse response = wsPlate.getPlateInfo(request);

                // Get the webservice proxy PlateInfo
                OPPF.Proxies.PlateInfo ppi = response.getPlateInfoReturn;

                // Map it into an IPlateInfo
                pi = new OPPF.Integrations.ImagerLink.PlateInfo();
                pi.DateDispensed = ppi.dateDispensed;
                pi.ExperimentName = ppi.experimentName;
                pi.PlateNumber = ppi.plateNumber;
                pi.PlateTypeID = ppi.plateTypeID;
                pi.ProjectName = ppi.projectName;
                pi.UserEmail = ppi.userEmail;
                pi.UserName = ppi.userName;

            }
            catch (Exception e)
            {
                string msg = "WSPlate.getPlateInfo threw " + e.GetType() + ":\n" + e.Message + "\nfor plate \"" + plateID + "\" in robot \"" + robot.Name + "\"\n - probably not in LIMS - not fatal.";
                msg = msg + WSPlateFactory.SoapExceptionToString(e);

                // Log it
                _log.Error(msg, e);

                // Don't rethrow - return null - don't want to stop imaging
            }

            // Return the IPlateInfo
            return pi;
             */
        }

        /// <summary>
        /// Retrieve a plate type. Rewritten to use the cached list of plate types.
        /// </summary>
        /// <param name="robot">The robot to find the plate type for.</param>
        /// <param name="plateTypeID">The ID of the plate type.</param>
        /// <returns>The plate type with ID of plateTypeID, or null if not found.</returns>
        public IPlateType GetPlateType(IRobot robot, string plateTypeID)
        {

            // Check arguments - do it up front to avoid possible inconsistencies later
            if (robot == null) throw new System.NullReferenceException("robot must not be null");
            if (plateTypeID == null) throw new System.NullReferenceException("plateTypeID must not be null");

            // Log the call
            if (_log.IsDebugEnabled)
            {
                string msg = "Called " + this + ".GetPlateType(robot=" + robot.ToString() + ", plateTypeID=\"" + plateTypeID + "\")";
                _log.Debug(msg);
            }

            IPlateType[] plateTypes = ((IPlateInfoProvider)this).GetPlateTypes(robot);
            if (null != plateTypes)
            {
                for (int i = 0; i < plateTypes.Length; i++)
                {
                    if (plateTypeID.Equals(plateTypes[i].ID))
                    {
                        return plateTypes[i];
                    }
                }

                // Should not get here - log error
                string msg = "Failed to find PlateType with ID: " + plateTypeID + " for robot \"" + robot.Name + "\" from " + plateTypes.Length + " plateTypes - returning null";
                _log.Error(msg);

            }

            else
            {
                // Should not get here - log error
                string msg = "Failed to find PlateType with ID: " + plateTypeID + " for robot \"" + robot.Name + "\" - GetPlateTypes returned null - returning null";
                _log.Error(msg);
            }

            // No better option than to return null
            return null;

        }

        /// <summary>
        /// Retrieve all the plate types. The list of PlateTypes is cached for _cacheLifetime min.
        /// </summary>
        /// <param name="robot">The robot to find the plate types for.</param>
        /// <returns>An array of plate types, or null if there are none.</returns>
        public IPlateType[] GetPlateTypes(IRobot robot)
        {

            // Check arguments - do it up front to avoid possible inconsistencies later
            if (robot == null) throw new System.NullReferenceException("robot must not be null");

            // Log the call
            if (_log.IsDebugEnabled)
            {
                string msg = "Called " + this + ".GetPlateTypes(robot=" + robot.ToString() + ")";
                _log.Debug(msg);
            }

            // Return cached values if appropriate
            if ((_plateTypes != null) && (System.DateTime.Now.Ticks <= _plateTypesCacheExpires))
            {
                _log.Debug("GetPlateTypes() using cached response");
                return _plateTypes;
            }

            _log.Debug("GetPlateTypes() refreshing cache");

            // Sychronize this block as we are interacting with the cache
            lock (_plateTypesLock)
            {

                try
                {
                    // Create and populate the request object
                    getPlateTypes request = new getPlateTypes();
                    request.robot = global::OPPF.Utilities.RobotUtils.createProxy(robot);

                    // Make the web service call
                    WSPlate wsPlate = new WSPlate();

                    // New stuff
                    wsPlate.Url = "http://localhost:8080/xtalpims-ws/services/WSPlate.WSPlateSOAP12port_http/";
                    ServicePointManager.Expect100Continue = false;
                    wsPlate.Credentials = new NetworkCredential("jon", "test123");
                    // End new stuff

                    getPlateTypesResponse response = wsPlate.getPlateTypes(request);

                    // Get the array of proxy PlateType[]
                    global::OPPF.Proxies.PlateType[] pptArray = response.wrapper;

                    // Map into an array of IPlateType[]
                    global::OPPF.Integrations.ImagerLink.PlateType[] iptArray = new global::OPPF.Integrations.ImagerLink.PlateType[pptArray.Length];
                    int i = 0;
                    foreach (global::OPPF.Proxies.PlateType ppt in pptArray)
                    {
                        iptArray[i] = new global::OPPF.Integrations.ImagerLink.PlateType();
                        iptArray[i].SetID(ppt.iD);
                        iptArray[i].SetName(ppt.name);
                        iptArray[i].SetNumColumns(ppt.numColumns);
                        iptArray[i].SetNumDrops(ppt.numDrops);
                        iptArray[i].SetNumRows(ppt.numRows);
                        i++;
                    }

                    // Copy into the cache and update cache expiry
                    _plateTypes = iptArray;
                    _plateTypesCacheExpires = System.DateTime.Now.Ticks + _cacheLifetime;

                    _log.Debug("GetPlateTypes() using fresh response");
                }
                catch (Exception e)
                {
                    // Log it
                    string msg = "WSPlate.getPlateTypes threw " + e.GetType() + ": " + e.Message + " for robot \"" + robot.Name + "\" - returning null";
                    msg = msg + WSPlateFactory.SoapExceptionToString(e);
                    _log.Error(msg, e);

                    // Don't rethrow - return cache (which might be null) - don't want to stop imaging
                }

            }

            // Return the array of IPlateType[]
            return _plateTypes;

        }

        #endregion

    }

}
