using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Unity.XR.XREAL.DrawingViewer
{
    /// <summary>
    /// Helpers for XR Hand subsystem pinch and joint queries.
    /// </summary>
    public static class HandTrackingUtility
    {
        private static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();

        public static XRHandSubsystem GetHandSubsystem()
        {
            Subsystems.Clear();
            SubsystemManager.GetSubsystems(Subsystems);

            foreach (var subsystem in Subsystems)
            {
                if (subsystem != null && subsystem.running)
                    return subsystem;
            }

            return null;
        }

        public static bool TryGetPinchMidpoint(
            Handedness handedness,
            float pinchThresholdMeters,
            out Vector3 midpoint,
            out float pinchStrength)
        {
            midpoint = Vector3.zero;
            pinchStrength = 0f;

            var subsystem = GetHandSubsystem();
            if (subsystem == null)
                return false;

            XRHand hand = handedness == Handedness.Right ? subsystem.rightHand : subsystem.leftHand;
            if (!hand.isTracked)
                return false;

            var indexJoint = hand.GetJoint(XRHandJointID.IndexTip);
            var thumbJoint = hand.GetJoint(XRHandJointID.ThumbTip);

            if (!indexJoint.TryGetPose(out Pose indexPose) || !thumbJoint.TryGetPose(out Pose thumbPose))
                return false;

            float distance = Vector3.Distance(indexPose.position, thumbPose.position);
            pinchStrength = Mathf.Clamp01(1f - distance / Mathf.Max(0.001f, pinchThresholdMeters * 2f));
            midpoint = (indexPose.position + thumbPose.position) * 0.5f;
            return distance <= pinchThresholdMeters;
        }

        public static bool TryGetPokeTip(Handedness handedness, out Vector3 tipPosition)
        {
            tipPosition = Vector3.zero;
            if (!TryGetPokePose(handedness, out Pose pose))
                return false;

            tipPosition = pose.position;
            return tipPosition != Vector3.zero;
        }

        public static bool TryGetPokePose(Handedness handedness, out Pose pose)
        {
            pose = Pose.identity;

            var subsystem = GetHandSubsystem();
            if (subsystem == null)
                return false;

            XRHand hand = handedness == Handedness.Right ? subsystem.rightHand : subsystem.leftHand;
            if (!hand.isTracked)
                return false;

            var indexJoint = hand.GetJoint(XRHandJointID.IndexTip);
            return indexJoint.TryGetPose(out pose);
        }

        public static Vector3 SmoothPosition(Vector3 current, Vector3 target, float smoothing)
        {
            float t = Mathf.Clamp01(1f - smoothing);
            return Vector3.Lerp(current, target, t);
        }
    }
}
