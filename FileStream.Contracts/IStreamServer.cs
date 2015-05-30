using System;
using System.IO;
using System.ServiceModel;

namespace FileStream.Contracts
{
    [ServiceContract(SessionMode = SessionMode.NotAllowed)]
    [ServiceKnownType(typeof(Stream))]
    [ServiceKnownType(typeof(StreamMessage))]
    [ServiceKnownType(typeof(StreamQuery))]
    public interface IStreamServer
    {
        [OperationContract]
        [FaultContract(typeof(FaultException))]
        Stream GetStream(Guid query);

        [OperationContract]
        [FaultContract(typeof(FaultException))]
        StreamMessage DownloadStream(StreamQuery streamQuery);

        [OperationContract/*(IsOneWay = true)*/]
        StreamInfo HasStream(StreamQuery message);

        [OperationContract/*(IsOneWay = true)*/]
        void PrepareStream(StreamMessage message);

        [OperationContract(IsOneWay = true)]
        void Stop();
    }
}