using System;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace FileStream.Common
{
    public class DataSetContractSurrogateFormatAttribute : Attribute, IContractBehavior, IOperationBehavior, IWsdlExportExtension
    {
        private DataSetContractSurrogate _surrogate;
        public DataSetContractSurrogate Surrogate
        {
            get { return _surrogate ?? (_surrogate = new DataSetContractSurrogate()); }
        }
        #region IOperationBehavior Members
        public void AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
        }

        public void ApplyClientBehavior(ContractDescription description, ServiceEndpoint endpoint, System.ServiceModel.Dispatcher.ClientRuntime proxy)
        {
            description.Operations.ForEach(ApplyDataContractSurrogate);
        }

        public void AddBindingParameters(ContractDescription contractDescription, ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void Validate(ContractDescription contractDescription, ServiceEndpoint endpoint)
        {
        }

        public void ApplyDispatchBehavior(ContractDescription description, ServiceEndpoint endpoint, System.ServiceModel.Dispatcher.DispatchRuntime dispatch)
        {
            description.Operations.ForEach(ApplyDataContractSurrogate);
        }

        private void ApplyDataContractSurrogate(OperationDescription description)
        {
            var dcsOperationBehavior = description.Behaviors.Find<DataContractSerializerOperationBehavior>();
            if (dcsOperationBehavior == null) return;

            if (dcsOperationBehavior.DataContractSurrogate == null)
                dcsOperationBehavior.DataContractSurrogate = Surrogate;
        }

        public void ApplyClientBehavior(OperationDescription description, System.ServiceModel.Dispatcher.ClientOperation proxy)
        {
            IOperationBehavior preservingDataContractSerializerOperationBehavior = new ReferencePreservingDataContractSerializerOperationBehavior(description);

            preservingDataContractSerializerOperationBehavior.ApplyClientBehavior(description, proxy);

            //ApplyDataContractSurrogate(description);
        }

        public void ApplyDispatchBehavior(OperationDescription description, System.ServiceModel.Dispatcher.DispatchOperation dispatch)
        {
            IOperationBehavior preservingDataContractSerializerOperationBehavior = new ReferencePreservingDataContractSerializerOperationBehavior(description);
            preservingDataContractSerializerOperationBehavior.ApplyDispatchBehavior(description, dispatch);

            //ApplyDataContractSurrogate(description);
        }

        public void Validate(OperationDescription description)
        {
        }

        #endregion

        public void ExportContract(WsdlExporter exporter, WsdlContractConversionContext context)
        {
            if (exporter == null)
                throw new ArgumentNullException("exporter");

            object dataContractExporter;
            XsdDataContractExporter xsdDCExporter;
            if (!exporter.State.TryGetValue(typeof(XsdDataContractExporter), out dataContractExporter))
            {
                xsdDCExporter = new XsdDataContractExporter(exporter.GeneratedXmlSchemas);
                exporter.State.Add(typeof(XsdDataContractExporter), xsdDCExporter);
            }
            else
            {
                xsdDCExporter = (XsdDataContractExporter)dataContractExporter;
            }
            if (xsdDCExporter.Options == null)
                xsdDCExporter.Options = new ExportOptions();

            if (xsdDCExporter.Options.DataContractSurrogate == null)
                xsdDCExporter.Options.DataContractSurrogate = Surrogate;
        }
        public void ExportEndpoint(WsdlExporter exporter, WsdlEndpointConversionContext context)
        {
        }
    }
}