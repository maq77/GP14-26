using SSSP.DAL.Enums;
using System;

namespace SSSP.BL.Records
{
    public sealed record CameraRecognitionPolicy(
    string CameraId,
    CameraRecognitionMode Mode,
    double EffectiveThreshold
    );
}
