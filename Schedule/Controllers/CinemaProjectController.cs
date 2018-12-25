using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Schedule.Classes;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace Schedule.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CinemaProjectController : ControllerBase
    {
        string connString;
        public CinemaProjectController(IConfiguration configuration)
        {
            connString = configuration.GetConnectionString("DB");
        }

        public string GetConectionString
        {
            get { return connString; }
        }

        [HttpGet("[action]")]
        public IEnumerable<Movie> GetMovies()
        {
            using (var sql = new SqlScriptExecutor(GetConectionString))
            {
                return sql.SelectRecords<Movie>("SELECT [ID],[Title] FROM [Movie] ORDER BY [Title]");
            }
        }
        [HttpGet("[action]")]
        public IEnumerable<MovieTheater> GetMovieTheaters()
        {
            using (var sql = new SqlScriptExecutor(GetConectionString))
            {
                return sql.SelectRecords<MovieTheater>("SELECT [ID],[Name] FROM [MovieTheater] ORDER BY [Name]");
            }
        }
        
        [HttpGet("[action]/{dateStr}")]
        public IEnumerable<ScheduleRow> GetSchedule(string dateStr)
        {
            DateTime date;

            if (!DateTime.TryParseExact(dateStr, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return new ScheduleRow[0];

            
            string cmdText = "SELECT s.[ID],s.[MovieTheater],[MovieTheaterName]=mt.[Name],s.[Movie],[MovieTitle]=m.[Title],s.[Date]" +
                " FROM [Schedule] s" +
                " INNER JOIN [MovieTheater] mt on s.[MovieTheater]=mt.[ID]" +
                " INNER JOIN [Movie] m on s.[Movie]=m.[ID]" +
                " WHERE [Date] = @date ORDER BY mt.[Name],m.[Title]";
            var cmd = new SqlCommand(cmdText);
            cmd.Parameters.Add(new SqlParameter("@date", date.Date));
            return SelectSchedule(cmd);
        }

        [HttpGet("[action]/{id}")]
        public ScheduleRow GetScheduleByID(int id)
        {
            string cmdText = "SELECT s.[ID],s.[MovieTheater],[MovieTheaterName]=mt.[Name],s.[Movie],[MovieTitle]=m.[Title],s.[Date]" +
              " FROM [Schedule] s" +
              " INNER JOIN [MovieTheater] mt on s.[MovieTheater]=mt.[ID]" +
              " INNER JOIN [Movie] m on s.[Movie]=m.[ID]" +
              " WHERE s.[ID] = @id";
            var cmd = new SqlCommand(cmdText);
            cmd.Parameters.Add(new SqlParameter("@id", id));
            return SelectSchedule(cmd).FirstOrDefault();
        }
        private ICollection<ScheduleRow> SelectSchedule(SqlCommand cmd)
        {
            ScheduleRow currentRow;
            string cmdText;
            using (var sql = new SqlScriptExecutor(GetConectionString))
            {
                ICollection<ScheduleRow> result = new LinkedList<ScheduleRow>(sql.SelectRecords<ScheduleRow>(cmd, (record)=>record.StartTimeList=new List<ScheduleTime>()));

                if (result.Count != 0)
                {
                    SortedList<int, ScheduleRow> orderedScheduleRows = new SortedList<int, ScheduleRow>(result.Count);
                    foreach (ScheduleRow row in result)
                    {
                        orderedScheduleRows.Add(row.ID, row);
                    }

                    currentRow = null;
                    ///Тут мы пытаемся избежать большого фильтра вида [ScheduleRow] in (), потому что SQL сервер будет долго парсить запрос.
                    ///и разбиваем на несколько запросов, пытаясь найти подряд идущие Id-шники, чтобы сформировать конструкцию Between.
                    ///но можно и не пытаться искать Between, а просто разбить на несколько in ().
                    foreach (string filter in SqlScriptExecutor.GetFilters(orderedScheduleRows.Keys, "[ScheduleRow]", 10, 1000, 20, 5000))
                    {
                        cmdText = "SELECT [ID],[ScheduleRow],[Time] FROM [ScheduleTime] WHERE " + filter + " ORDER BY [ScheduleRow], [Time]";
                        foreach (ScheduleTime currentTime in sql.SelectRecords<ScheduleTime>(cmdText))
                        {
                            if (currentRow == null || currentRow.ID != currentTime.ScheduleRow)
                            {
                                currentRow = orderedScheduleRows[currentTime.ScheduleRow];
                            }
                            currentRow.StartTimeList.Add(currentTime);
                        }
                    }
                }
                return result;
            }
        }

        [HttpPost]
        public void Post([FromBody] ScheduleRow value)
        {
            DateTime date;
            int ScheduleRowID;
            date = value.Date;
            using (var sql = new SqlScriptExecutor(GetConectionString))
            {
                sql.BeginTransaction();

                List<KeyValuePair<string, object>> values = new List<KeyValuePair<string, object>>()
                {
                    new KeyValuePair<string, object>("Movie", value.Movie),
                    new KeyValuePair<string, object>("MovieTheater", value.MovieTheater),
                    new KeyValuePair<string, object>("Date", date)
                };

                ScheduleRowID = sql.InsertValues("Schedule", values);

                values.Clear();
                values.Add(new KeyValuePair<string, object>("ScheduleRow", ScheduleRowID));
                values.Add(new KeyValuePair<string, object>());

                foreach (var scheduleTime in value.StartTimeList)
                {
                    values[1] = new KeyValuePair<string, object>("Time", scheduleTime.Time);
                    sql.InsertValues("ScheduleTime", values);
                }

                sql.CommitTransaction();
            }
        }

        // PUT: api/CinemaProject/5
        [HttpPut("{id:int}")]
        public void Put(int id, [FromBody] ScheduleRow value)
        {
            DateTime date;
            int ScheduleRowID = id;
            date = value.Date;
            using (var sql = new SqlScriptExecutor(GetConectionString))
            {
                sql.BeginTransaction();

                //Сохраним саму запись
                List<KeyValuePair<string, object>> values = new List<KeyValuePair<string, object>>()
                {
                    new KeyValuePair<string, object>("Movie", value.Movie),
                    new KeyValuePair<string, object>("MovieTheater", value.MovieTheater),
                    new KeyValuePair<string, object>("Date", date)
                };
                sql.UpdateValues("Schedule", id, values);
                values.Clear();
                values.Add(new KeyValuePair<string, object>("ScheduleRow", ScheduleRowID));
                values.Add(new KeyValuePair<string, object>());

                LinkedList<int> timesToDelete = new LinkedList<int>();//Список элементов на удаление
                LinkedList<ScheduleTime> timesToUpdate = new LinkedList<ScheduleTime>();//Список элементов на обновление
                ScheduleTime[] timesToInsert = value.StartTimeList.Where(it => it.ID == 0).ToArray();//Список элементов на добавление

                //Просмотрим существующие записи о времени. Вслучае, если передаваемые записи на изменение были удалены,
                //Запретим менять запись, выкинув Exception.
                const string cmdText = "SELECT [ID] FROM [ScheduleTime] WHERE [ScheduleRow] = @id ORDER BY [ID]";
                using (SqlCommand cmd = new SqlCommand(cmdText))
                {
                    cmd.Parameters.Add(new SqlParameter("@id", id));

                    //Упорядочим идентификаторы существующих записей для сравнения с записями в БД
                    ScheduleTime[] orderedTime = value.StartTimeList.Where(it => it.ID != 0).OrderBy(it => it.ID).ToArray();
                    int i = 0;
                    int valueFromReader = -1;
                    ScheduleTime currentTime = null;
                    //Сравним две упорядоченные цепочки идентификаторов
                    using (var reader = sql.ExecuteReader(cmd))
                    {
                        bool readerEnd = !reader.Read();
                        bool localValuesEnd = i == orderedTime.Length;

                        while (!readerEnd && !localValuesEnd)
                        {
                            valueFromReader = reader.GetInt32(0);
                            currentTime = orderedTime[i];
                            if (currentTime.ID == valueFromReader)//Оба значения есть и там и там, всё впорядке, обновим запись. Двигаем обе цепочки
                            {
                                timesToUpdate.AddLast(currentTime);
                                readerEnd = !reader.Read();
                                localValuesEnd = ++i == orderedTime.Length;
                            }
                            else if (currentTime.ID < valueFromReader) //Записи в БД нет, то есть она была удалена за время редактирования. Продолжение невозможно. Выкидываем Exception
                            {
                                throw new Exception("Информация устарела, обновите редактор");
                            }
                            else //(currentTime.ID > valueFromReader) //Есть запись в БД, которой нет локально - Двигаем reader
                            {
                                timesToDelete.AddLast(valueFromReader);
                                readerEnd = !reader.Read();
                            }
                        }
                        if (!readerEnd) //Если остались записи в БД, которых нет локально
                        {
                            do
                            {
                                timesToDelete.AddLast(reader.GetInt32(0));
                            }
                            while (reader.Read());
                        }
                        else if (!localValuesEnd) //Остались записи, которых нет в БД
                        {
                            throw new Exception("Информация устарела, обновите редактор");
                        }
                    }
                }

                foreach(var currentID in timesToDelete)
                {
                    sql.DeleteValues("ScheduleTime", currentID);
                }

                foreach (var currentTime in timesToUpdate)
                {
                    values[1] = new KeyValuePair<string, object>("Time", currentTime.Time);
                    sql.UpdateValues("ScheduleTime", currentTime.ID, values);
                }

                foreach (var currentTime in timesToInsert)
                {
                    values[1] = new KeyValuePair<string, object>("Time", currentTime.Time);
                    sql.InsertValues("ScheduleTime", values);
                }
                
                sql.CommitTransaction();
            }
        }

        // DELETE: api/CinemaProject/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
            using (var sql = new SqlScriptExecutor(GetConectionString))
            {
                sql.BeginTransaction();
                sql.DeleteValues("Schedule", id);//Остальные записи удалятся по связям настроенным в БД
                sql.CommitTransaction();
            }
        }
    }
}
