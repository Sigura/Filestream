using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel.Description;
using System.Xml;

namespace FileStream.Common
{
    public class ReferencePreservingDataContractSerializerOperationBehavior : DataContractSerializerOperationBehavior
    {
        public ReferencePreservingDataContractSerializerOperationBehavior(OperationDescription operationDescription)
            : base(operationDescription)
        {
            //Logger.Instance.Info(@"ReferencePreservingDataContractSerializerOperationBehavior created");
        }

        public override XmlObjectSerializer CreateSerializer(Type type, XmlDictionaryString name, XmlDictionaryString ns, IList<Type> knownTypes)
        {
            return new DataContractSerializer(type, name, ns, knownTypes,
                32767 /*maxItemsInObjectGraph*/,
                false /*ignoreExtensionDataObject*/,
                true  /*preserveObjectReferences*/,
                null  /*dataContractSurrogate*/);
        }
    }
}