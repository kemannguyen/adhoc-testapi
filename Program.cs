using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// Load example data
var alarmLog = JsonSerializer.Deserialize<IReadOnlyList<AlarmLog>>(File.ReadAllText("alarmlog.json"));
var alarms = JsonSerializer.Deserialize<IReadOnlyList<Alarm>>(File.ReadAllText("alarms.json"));

// Set up endpoints
app.MapGet("/alarms", () =>
{
    var models = new List<AlarmModel>();
    foreach (var alarm in alarms)
    {
        models.Add(
            new AlarmModel
            {
                Id = alarm.AlarmId,
                Station = alarm.Station,
                Number = alarm.AlarmNumber,
                Class = alarm.AlarmClass,
                Text = alarm.AlarmText,
            }
        );
    }

    return models;
})
.WithName("Alarms");

app.MapGet("/alarmLog", () =>
{
    return alarmLog;
})
.WithName("AlarmLog");

app.MapGet("/activations_per_alarm", () =>
{
    var models = new Dictionary<string ,ActivationModelAlarm>();
    foreach (var alarm in alarms)
    {
        //populate models with all the alarms, no "else" since it's assumed that all alarms are unique
        //necessary to have activations per alarm at 0
        if(!models.ContainsKey($"{alarm.Station} {alarm.AlarmText}"))
        {
            models[$"{alarm.Station} {alarm.AlarmText}"] =
            new ActivationModelAlarm 
            {
                Station = alarm.Station,
                Text = alarm.AlarmText,
                Count = 0,
            };
        }
    }

    //increase the count or an alarm activation if there is a alarmLog with the same id
    foreach (var log in alarmLog)
    {
        var currAlarm = alarms.FirstOrDefault( x => x.AlarmId == log.AlarmId);
        //assuming that it's an new alarm activation everytime the logged alarm event == on 
        if(models.ContainsKey($"{currAlarm.Station} {currAlarm.AlarmText}") && log.Event == LoggedAlarmEvent.On)
        {
            models[$"{currAlarm.Station} {currAlarm.AlarmText}"].Count++;
        }
    }

    return models.Values.ToList().OrderByDescending(x=> x.Count);
})
.WithName("Activations_per_alarm");

app.MapGet("/activations_per_station", () =>
{
    var models =  new Dictionary<string ,ActivationModel>();
    foreach (var alarm in alarms)
    {
        //populate models with all the alarms, no "else" since it's assumed that all alarms are unique
        //necessary to have activations per station at 0
        if(!models.ContainsKey(alarm.Station))
        {
            models[alarm.Station] =
            new ActivationModel 
            {
                Label = alarm.Station,
                Count = 0,
            };
        }
    }

     foreach (var log in alarmLog)
    {
        var currAlarm = alarms.FirstOrDefault( x => x.AlarmId == log.AlarmId);

          //assuming that is an new alarm activation if alarmLog returns event 1, even if it was previously 1
        if(models.ContainsKey(currAlarm.Station) && log.Event == LoggedAlarmEvent.On)
        {
            models[currAlarm.Station].Count++;
        }
    }

    return models.Values.ToList().OrderByDescending(x=> x.Count);
})
.WithName("Activations_per_station");

app.MapGet("/status_summary", () =>
{   
    var model = new SummaryModel();
    //alarm id, event
    var activeAlarms = new Dictionary<int, int>();

    //reverse for chronical order to correcly find how many alarms that are active
    foreach (var log in alarmLog.Reverse())
    {
        //handle currently active alarms
        //increase alarm count if it was off before
        if(log.Event == LoggedAlarmEvent.On){
            if(!activeAlarms.ContainsKey(log.AlarmId))
            {
                activeAlarms[log.AlarmId] = ((int)log.Event);
                model.CurrActiveAlarms++;
                model.AlarmActivations++;
            }
        }
        //decrease alarm count if it was on before
        if(log.Event == LoggedAlarmEvent.Off)
        {   
            //can only turn on if it has been off and vice versa
            if(activeAlarms.ContainsKey(log.AlarmId))
            {
                activeAlarms.Remove(log.AlarmId);
                model.CurrActiveAlarms--;
            }
        }

        //increase total paging if log event is 8, 10, 11 or 12 (assuming total pagings refers to pagings sent)
        if(((int)log.Event) >= 8 && ((int)log.Event) <13)
        {
            if(((int)log.Event) != 9)
            {
                model.TotalNumPagings++;
            }
        }

    }
    return model;
})
.WithName("Status_summary");

app.Run();

// Output models
public class AlarmModel
{
    public int Id { get; set; }

    public string Station { get; set; }

    public int Number { get; set; }

    public string Class { get; set; }

    public string Text { get; set; }
}

public class ActivationModelAlarm
{
    public string Station {get; set; }

    public string Text {get; set; } 

    public int Count {get; set;}
}
public class ActivationModel
{
    public string Label {get; set; }

    public int Count {get; set;}
}

public class SummaryModel
{
    public int CurrActiveAlarms {get; set;}

    public int AlarmActivations {get; set;}
    public int TotalNumPagings {get; set;}
}

// Data models
public record AlarmLog
{
    public int AlarmId { get; init; }

    public LoggedAlarmEvent Event { get; init; }

    public string AckBy { get; init; }

    public DateTime Date { get; init; }
}

public record Alarm
{
    public int AlarmId { get; init; }

    public string Station { get; init; }

    public int AlarmNumber { get; init; }

    public string AlarmClass { get; init; }

    public string AlarmText { get; init; }
}

public enum LoggedAlarmEvent
{
    Off = 0,
    On = 1,
    Acked = 2,
    Blocked = 3,
    UnBlocked = 4,
    AckedLocally = 5,
    Cause = 6,
    Reset = 7,
    PagingSentToUser = 8,
    PagingUserSMSReceived = 9,
    PagingSentSMSToUser = 10,
    PagingSentMailToUser = 11,
    PagingSentPushToUser = 12,
    PagingUserPushReceived = 13,
    PagingUserPushRead = 14,
}