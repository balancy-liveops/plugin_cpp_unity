using System;
using System.Runtime.InteropServices;
using System.Text;
using Balancy.Core;
using UnityEngine;

namespace Balancy.XOffers
{
    internal static class XOffersInterop
    {
        private static string Utf8(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return null;
            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0) length = checked(length + 1);
            var bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
        internal static T Read<T>(IntPtr pointer) where T : Responses.ResponseData, new()
        {
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("Null XOffers response");
            var result = new T
            {
                Success = Marshal.ReadByte(pointer) == 1,
                ErrorCode = Marshal.ReadInt32(pointer, 1),
                ErrorMessage = Utf8(Marshal.ReadIntPtr(pointer, 5))
            };
            if (!result.Success) return result;
            var json = Utf8(Marshal.ReadIntPtr(pointer, 5 + IntPtr.Size));
            var contextJson = Utf8(Marshal.ReadIntPtr(pointer, 5 + 2 * IntPtr.Size));
            if (string.IsNullOrEmpty(json)) throw new InvalidOperationException("Missing XOffers response data");
            var wire = JsonUtility.FromJson<XOffersWireResponse>(json);
            if (wire == null) throw new InvalidOperationException("Invalid XOffers response data");
            if (result is XOffersContextResponse context)
            {
                if (wire.context == null || string.IsNullOrEmpty(contextJson)) throw new InvalidOperationException("Missing XOffers context");
                context.BaseUrl = wire.baseUrl; context.SourceEnabled = wire.sourceEnabled; context.TargetEnabled = wire.targetEnabled;
                context.Context = wire.context; context.ContextJson = contextJson;
            }
            else if (result is XOffersAssignmentsResponse assignments)
            {
                if (wire.assignments == null) throw new InvalidOperationException("Missing XOffers assignments");
                assignments.RequestId = wire.request_id; assignments.OriginOfferId = wire.origin_offer_id;
                assignments.Assignments = Array.AsReadOnly(wire.assignments);
            }
            else if (result is XOffersCompletionResponse completion)
            {
                completion.RequestId = wire.request_id; completion.AssignmentId = wire.assignment_id;
                completion.State = XOffersAssignment.ParseState(wire.state); completion.CompletedAt = wire.completed_at;
                completion.Pending = wire.pending; completion.Duplicate = wire.duplicate;
            }
            return result;
        }
    }
}
