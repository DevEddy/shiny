using System;
using CoreLocation;

namespace Shiny.Locations;


static class PlatformExtensions
{

    public static Position FromNative(this CLLocationCoordinate2D native)
        => new Position(native.Latitude, native.Longitude);


    public static GpsReading FromNative(this CLLocation location) => new GpsReading(
        location.Coordinate.FromNative(),
        location.HorizontalAccuracy,
        DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(location.Timestamp.SecondsSince1970)),
        location.Course,
        location.VerticalAccuracy,
        location.Altitude,
        location.Speed,
        location.SpeedAccuracy
    );


    public static GeofenceState FromNative(this CLRegionState state) => state switch
    {
         CLRegionState.Inside => GeofenceState.Entered,
         CLRegionState.Outside => GeofenceState.Exited,
         _ => GeofenceState.Unknown
    };


    public static CLLocationCoordinate2D ToNative(this Position position)
        => new CLLocationCoordinate2D(position.Latitude, position.Longitude);


    public static CLCircularRegion ToNative(this GeofenceRegion region)
        => new CLCircularRegion
        (
            region.Center.ToNative(),
            region.Radius.TotalMeters,
            region.Identifier
        )
        {
            NotifyOnEntry = region.NotifyOnEntry,
            NotifyOnExit = region.NotifyOnExit
        };

    /// <summary>
    /// https://developer.apple.com/documentation/corelocation/clmonitoringevent
    /// </summary>
    /// <param name="evt">Instances of CLMonitoringEvent contain detailed information about an event in the monitoring of a CLCondition by a CLMonitor.</param>
    /// <returns>A readable info based on the documentation</returns>
    public static string GetReadableInfo(this CLMonitoringEvent evt)
    {
        // A string that represents the identifier of a monitored condition.
        var identifier = evt.Identifier;
        
        // A Boolean value that indicates whether the app receives accuracy-limited location updates.
        var isAccuracyLimited = evt.AccuracyLimited;
        
        // A Boolean value that indicates whether the app has local authorization.
        var isAuthorizationDenied = evt.AuthorizationDenied;
        
        // A Boolean value that indicates whether the app has system-wide authorization.
        var isAuthorizationDeniedGlobally = evt.AuthorizationDeniedGlobally;
        
        // A Boolean value that indicates whether the app can make authorization changes.
        var isAuthorizationRestricted = evt.AuthorizationRestricted;
        
        // A Boolean value that indicates whether the app receives location updates based on other monitoring conditions.
        var conditionLimitExceeded = evt.ConditionLimitExceeded;
        
        // A Boolean value that indicates whether the app receives location updates based on the supported condition.
        var conditionUnsupported = evt.ConditionUnsupported;
        
        // A Boolean value that indicates whether the app receives location updates because it’s insufficiently in use.
        var isInsufficientlyInUse = evt.InsufficientlyInUse;
        
        // A Boolean value that indicates whether it receives location updates based on successful persistence.
        var isPersistenceUnavailable = evt.PersistenceUnavailable;
        
        return $"Identifier: {identifier}, AccuracyLimited: {isAccuracyLimited}, AuthorizationDenied: {isAuthorizationDenied}, AuthorizationDeniedGlobally: {isAuthorizationDeniedGlobally}, AuthorizationRestricted: {isAuthorizationRestricted}, ConditionLimitExceeded: {conditionLimitExceeded}, ConditionUnsupported: {conditionUnsupported}, InsufficientlyInUse: {isInsufficientlyInUse}, PersistenceUnavailable: {isPersistenceUnavailable}";
    }
}
