using System;
using System.CodeDom;
using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;
using System.Runtime.Serialization;

namespace FileStream.Common
{
    public class DataSetContractSurrogate : IDataContractSurrogate
    {
        public Type GetDataContractType(Type type)
        {
            return typeof(DataSet).IsAssignableFrom(type) ? typeof(DataSetSurrogate) : type;
        }

        public object GetObjectToSerialize(object obj, Type targetType)
        {
            //if (_originalType == obj.GetType())
            //    Logger.Instance.Info(@"serialized type is {0}", obj.GetType().FullName);
            return obj is DataSet ? new DataSetSurrogate((DataSet)obj) : obj;
        }

        public object GetDeserializedObject(object obj, Type targetType)
        {
            var orig = obj as DataSetSurrogate;

            //if (orig != null)
            //    Logger.Instance.Info(@"deserialized {0}", orig.GetType().FullName);
            return orig != null ? orig.ConvertToDataSet() : obj;
        }

        public object GetCustomDataToExport(MemberInfo memberInfo, Type dataContractType)
        {
            return null;
        }

        public object GetCustomDataToExport(Type clrType, Type dataContractType)
        {
            return null;
        }

        public void GetKnownCustomDataTypes(Collection<Type> customDataTypes)
        {
        }

        public Type GetReferencedTypeOnImport(string typeName, string typeNamespace, object customData)
        {
            if (typeName.Equals("DataSetSurrogate"))
                return typeof(DataSet);

            return null;
        }

        public CodeTypeDeclaration ProcessImportedType(CodeTypeDeclaration typeDeclaration, CodeCompileUnit compileUnit)
        {
            return typeDeclaration;
        }
    }
}