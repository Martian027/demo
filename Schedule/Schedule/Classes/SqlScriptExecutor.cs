using Microsoft.IdentityModel.Protocols;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Schedule.Classes
{
    public class SqlScriptExecutor : IDisposable
    {
        string connectionString;
        SqlConnection connection;
        SqlTransaction transaction;

        public SqlScriptExecutor(string connectionString)
        {
            this.connectionString = connectionString;
        }

        #region Транзакции
        /// <summary>
        /// Начинает транзакцию, вложенные транзакции не поддерживаются
        /// </summary>
        public void BeginTransaction()
        {
            PrepareConnection();
            if (transaction == null)
                transaction = connection.BeginTransaction();
            else
                throw new Exception("Вложенные транзакции не поддерживаются");
        }

        public void RollbackTransaction()
        {
            if (transaction != null)
            {
                transaction.Rollback();
                transaction = null;
            }
        }

        public void CommitTransaction()
        {
            if (transaction != null)
            {
                transaction.Commit();
                transaction = null;
            }
        }

        #endregion

        private void PrepareConnection()
        {
            if (connection == null)
            {
                connection = new SqlConnection(connectionString);
                connection.Open();
            }
        }
        const int SQL_COMMAND_TIMEOUT = 0;
        private void PrepareCommand(SqlCommand cmd)
        {
            PrepareConnection();
            cmd.Transaction = transaction;
            cmd.CommandTimeout = SQL_COMMAND_TIMEOUT;
            cmd.Connection = connection;
        }



        #region Выполнение запросов к БД
        public void ExecuteNonQuery(string commandText)
        {
            SqlCommand cmd = new SqlCommand(commandText, connection);
            ExecuteNonQuery(cmd);
        }

        public void ExecuteNonQuery(SqlCommand cmd)
        {
            try
            {
                PrepareCommand(cmd);
                cmd.ExecuteNonQuery();
            }
            finally
            {
                cmd.Dispose();
            }
        }

        public T ExecuteScalar<T>(string commandText, T defaultValue = default(T))
        {
            SqlCommand cmd = new SqlCommand(commandText, connection);
            return ExecuteScalar<T>(cmd);
        }
        public T ExecuteScalar<T>(SqlCommand cmd, T defaultValue = default(T))
        {
            try
            {
                PrepareConnection();
                object result;
                result = cmd.ExecuteScalar();
                if (result is T)
                    return (T)result;
                return defaultValue;
            }
            finally
            {
                cmd.Dispose();
            }
        }


        public SqlDataReader ExecuteReader(string commandText)
        {
            PrepareConnection();
            using (SqlCommand cmd = new SqlCommand(commandText, connection))
            {
                return cmd.ExecuteReader();
            }
        }

        public SqlDataReader ExecuteReader(SqlCommand cmd)
        {
            PrepareCommand(cmd);
            return cmd.ExecuteReader();
        }
        #endregion


        public int InsertValues(string tableName, IList<KeyValuePair<string, object>> values)
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                string[] fieldsStr = new string[values.Count];
                string[] valuesStr = new string[values.Count];
                int i = 0;
                foreach (var keyValue in values)
                {
                    fieldsStr[i] = "[" + keyValue.Key + "]";
                    valuesStr[i] = "@value" + i;
                    cmd.Parameters.Add(new SqlParameter("@value" + i, keyValue.Value));
                    i++;
                }
                cmd.CommandText = "INSERT [" + tableName + "]  (" + string.Join(',', fieldsStr) + ") VALUES (" + string.Join(',', valuesStr) + "); SELECT CAST(SCOPE_IDENTITY() AS INT);";
                PrepareCommand(cmd);
                return (int)cmd.ExecuteScalar();
            }
        }
        public void UpdateValues(string tableName, int id, IList<KeyValuePair<string, object>> values)
        {
            if (values.Count == 0)
                return;
            using (SqlCommand cmd = new SqlCommand())
            {
                string[] fieldsStr = new string[values.Count];
                int i = 0;
                foreach (var keyValue in values)
                {
                    fieldsStr[i] = "[" + keyValue.Key + "]=@value" + i;
                    cmd.Parameters.Add(new SqlParameter("@value" + i, keyValue.Value));
                    i++;
                }
                cmd.CommandText = "UPDATE [" + tableName + "] SET " + string.Join(',', fieldsStr) + " WHERE ID=@id;";
                cmd.Parameters.Add(new SqlParameter("@id", id));
                PrepareCommand(cmd);
                cmd.ExecuteNonQuery();
            }
        }
        public void DeleteValues(string tableName, int id)
        {
            using (SqlCommand cmd = new SqlCommand())
            {
                cmd.CommandText = "DELETE [" + tableName + "] WHERE [ID]=@id;";
                cmd.Parameters.Add(new SqlParameter("@id", id));
                PrepareCommand(cmd);
                cmd.ExecuteNonQuery();
            }
        }
        public void DeleteValues(string tableName, IList<int> ids)
        {
            if (ids.Count == 0)
                return;
            string parName;
            StringBuilder sb = new StringBuilder(ids.Count);
            using (SqlCommand cmd = new SqlCommand())
            {
                int i;
                for (i = 0; i < ids.Count; i++)
                {
                    parName = "@id" + i;
                    if (i == 0)
                        sb.Append(parName);
                    else
                        sb.Append("," + parName);
                    cmd.Parameters.Add(new SqlParameter(parName, ids[i]));
                }
                cmd.CommandText = "DELETE [" + tableName + "] WHERE [ID] in (" + sb.ToString() + ");";
                PrepareCommand(cmd);
                cmd.ExecuteNonQuery();
            }
        }

        #region Формирование экземпляров класса
        public T CreateRecord<T>(SqlDataReader reader, Action<T> initializeAction) where T:new()
        {
            T record = new T();
            if (initializeAction != null)
                initializeAction(record);
            Object obj;

            string FieldName;

            for (int i = 0; i < reader.FieldCount; i++)
            {
                FieldName = reader.GetName(i);
                if ((obj = reader[FieldName]) != null)
                {
                    SetPropertyValue(record, FieldName, obj);
                }
            }
            return record;
        }

        ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>> TypesProperties = new ConcurrentDictionary<Type, Dictionary<string, PropertyInfo>>();
        static Type nullableTypeDefinition = typeof(Nullable<>);
        public void SetPropertyValue(object obj, string PropertyPath, object value)
        {
            Dictionary<string, PropertyInfo> properties;
            PropertyInfo property;
            Type T = obj.GetType();
            Type genericType;
            if (!TypesProperties.TryGetValue(T, out properties))
            {
                properties = T.GetProperties().Where(it => it.CanRead && it.CanWrite).ToDictionary(it => it.Name, StringComparer.CurrentCultureIgnoreCase);
                TypesProperties.TryAdd(T, properties);
            }

            int dotIndex = PropertyPath.IndexOf('.');
            if (dotIndex < 0)
            {
                if (properties.TryGetValue(PropertyPath, out property))
                {
                    if (value.GetType() == property.PropertyType || (value.GetType() == typeof(int) && property.PropertyType.IsEnum))
                    {
                        property.SetValue(obj, value, null);
                    }
                    else
                    {
                        try
                        {
                            if (property.PropertyType.IsGenericType)
                            {
                                genericType = property.PropertyType.GetGenericTypeDefinition();
                               if (genericType == nullableTypeDefinition)
                                {
                                    if (value is DBNull)
                                        property.SetValue(obj, null);
                                    else
                                    {
                                        var nullableConverter = new NullableConverter(property.PropertyType);
                                        property.SetValue(obj, Convert.ChangeType(value, nullableConverter.UnderlyingType), null);
                                    }
                                    return;
                                }
                                
                            }
                          
                            property.SetValue(obj, Convert.ChangeType(value, property.PropertyType), null);
                        }
                        catch (Exception Ex)
                        {
                            throw new Exception(Ex.Message + Environment.NewLine + string.Format("[{0}] = '{1}'", PropertyPath, value));
                        }
                    }
                }
            }
            else
            {
                string curPropertyName = PropertyPath.Substring(0, dotIndex);
                if (properties.TryGetValue(curPropertyName, out property))
                {
                    string NextPropertyPath = PropertyPath.Substring(dotIndex + 1);
                    object NextObject = property.GetValue(obj, null);
                    if (NextObject == null)
                    {
                        NextObject = CreateRecord(property.PropertyType);
                        property.SetValue(obj, NextObject, null);
                    }
                    SetPropertyValue(NextObject, NextPropertyPath, value);
                }
            }
        }

        public object CreateRecord(Type T)
        {
            return Activator.CreateInstance(T);
        }

        #endregion
        public IEnumerable<T> SelectRecords<T>(string cmdText) where T : BaseRecord, new()
        {
            var cmd = new SqlCommand(cmdText);
            return SelectRecords<T>(cmd);
        }
        public IEnumerable<T> SelectRecords<T>(SqlCommand cmd, Action<T> initializeAction=null) where T : BaseRecord, new()
        {
            try
            {
                PrepareCommand(cmd);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        yield return CreateRecord<T>(reader, initializeAction);
                }
            }
            finally
            {
                cmd.Dispose();
            }
        }

        //public T SelectRecord<T>(string tableName, int id) where T : BaseRecord
        //{

        //}

        //public List<T> SelectRecordsByFilter

        /// <summary>
        /// Сформировать фильтры, необходимые для получения элементов
        /// </summary>
        /// <param name="items">Упорядоченная по возрастанию уникальная коллекция целых чисел</param>
        /// <param name="fieldName">Имя поля по которому формируется фильтр</param>
        /// <param name="minRangeLength">Минимальное количество идущих подряд элементов, которые следует объеденить в конструкцию BETWEEN</param>
        /// <param name="maxSingleItemsCount">Максимальное количество элементов в конструкции IN</param>
        /// <param name="maxRangeCount">Максимальное количество конструкций BETWEEN, которые следует объединить в конструкцию OR</param>
        /// <param name="maxRangeItemsCount">Если суммарное количество элементов, входящих в конструкцию BETWEEN превышает этот параметр, конструкция OR не будет дожидаться достижения MAX_RANGES_COUNT</param>
        /// <returns>Коллекцию сформированных строковых фильтров в формате SQL</returns>
        public static IEnumerable<string> GetFilters(IEnumerable<int> items, string fieldName, int minRangeLength, int maxSingleItemsCount, int maxRangeCount, int maxRangeItemsCount)
        {
            int min = 0;
            int max = 0;
            int current;
            int i;
            List<int> SingleItems = new List<int>(maxSingleItemsCount);
            List<string> Ranges = new List<string>(maxRangeCount);
            int RangeElements = 0;
            IEnumerator<int> enumerator = items.GetEnumerator();
            bool hasElements;
            bool endOfRange;
            if (enumerator.MoveNext())
            {
                current = enumerator.Current;
                min = max = current;
                endOfRange = false;
                do
                {
                    hasElements = enumerator.MoveNext();
                    if (hasElements)
                    {
                        current = enumerator.Current;
                        if (max + 1 == current)
                            max = current;
                        else
                            endOfRange = true;
                    }
                    if (!hasElements)
                        endOfRange = true;

                    if (endOfRange)
                    {
                        if (max - min + 1 < minRangeLength)
                        {
                            for (i = min; i <= max; i++)
                            {
                                SingleItems.Add(i);
                                if (SingleItems.Count == maxSingleItemsCount)// || !hasElements && i == max)
                                {
                                    yield return fieldName + " in (" + string.Join(",", SingleItems) + ")";
                                    SingleItems.Clear();
                                }
                            }
                        }
                        else
                        {
                            Ranges.Add(fieldName + " BETWEEN " + min.ToString() + " AND " + max.ToString());
                            //Ranges.Add(fieldName + ">=" + min.ToString() + " AND "+ fieldName+"<=" + max.ToString());
                            //Ranges.Add("[" + min.ToString() + " ; " + max.ToString() + "]");
                            RangeElements += max - min + 1;
                            if (Ranges.Count == maxRangeCount)//|| RangeElements >= maxRangeItemsCount || !hasElements)
                            {
                                yield return string.Join(" or ", Ranges);
                                Ranges.Clear();
                                RangeElements = 0;
                            }
                        }

                        min = max = current;
                        endOfRange = false;
                    }

                }
                while (hasElements);

                string SingleItemsString = string.Empty;
                string RangeString = string.Empty;

                if (SingleItems.Count != 0)
                {
                    if (SingleItems.Count == 1)
                        SingleItemsString = fieldName + "=" + SingleItems[0].ToString();
                    else
                        SingleItemsString = fieldName + " in (" + string.Join(",", SingleItems.Select(it => it.ToString())) + ")";
                }
                if (Ranges.Count != 0)
                    RangeString = string.Join(" or ", Ranges);

                if (string.IsNullOrEmpty(RangeString))
                {
                    if (!string.IsNullOrEmpty(SingleItemsString))
                        yield return SingleItemsString;
                }
                else
                {
                    if (string.IsNullOrEmpty(SingleItemsString))
                        yield return RangeString;
                    else
                        yield return RangeString + " or " + SingleItemsString;
                }


            }
        }
        
        public void Dispose()
        {
            if (transaction != null)
            {
                transaction.Rollback();
                transaction.Dispose();
                transaction = null;
            }
            if (connection != null)
            {
                connection.Close();
                connection.Dispose();
                connection = null;
            }
        }
    }
}
