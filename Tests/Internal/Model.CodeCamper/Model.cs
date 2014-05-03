using System.IO;

using Breeze.Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Model.CodeCamper {

  public class Person : BaseEntity {
    public Int32 Id {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
    public String FirstName {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String LastName {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String Email {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String Blog {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String Twitter {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String Gender {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String ImageSource {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public NavigationSet<Session> SpeakerSessions {
      get { return GetValue<NavigationSet<Session>>(); }
      set { SetValue(value); }
    }
  }
  
  public class Session : BaseEntity {
    public Int32 Id {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
    public String Title {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String Code {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String Description {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String Level {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public String Tags {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
    public Int32 RoomId {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
    public Int32 SpeakerId {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
    public Int32 TimeSlotId {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
    public Int32 TrackId {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
    public Room Room {
      get { return GetValue<Room>(); }
      set { SetValue(value); }
    }
    public Person Speaker {
      get { return GetValue<Person>(); }
      set { SetValue(value); }
    }
    public TimeSlot TimeSlot {
      get { return GetValue<TimeSlot>(); }
      set { SetValue(value); }
    }
    public Track Track {
      get { return GetValue<Track>(); }
      set { SetValue(value); }
    }
  }

  public class Room : BaseEntity {
    public Int32 Id {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
    public String Name {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
  }

  public class TimeSlot : BaseEntity {
    public Int32 Id {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
    public DateTime Start {
      get { return GetValue<DateTime>(); }
      set { SetValue(value); }
    }
    public bool IsSessionSlot {
      get { return GetValue<bool>(); }
      set { SetValue(value); }
    }
    public Int32 Duration {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
  }

  public class Track : BaseEntity {
    public Int32 Id {
      get { return GetValue<Int32>(); }
      set { SetValue(value); }
    }
    public String Name {
      get { return GetValue<String>(); }
      set { SetValue(value); }
    }
  }

  public static class Config {

    public static void Initialize() {

      // Configure keys and relationships
      var personBuilder = new EntityTypeBuilder<Person>();
      personBuilder.DataProperty(person => person.Id).IsPartOfKey();

      var sessionBuilder = new EntityTypeBuilder<Session>();
      sessionBuilder.DataProperty(session => session.Id).IsPartOfKey().IsAutoIncrementing();
      sessionBuilder.NavigationProperty(session => session.Room)
        .HasForeignKey(session => session.RoomId);
      sessionBuilder.NavigationProperty(session => session.TimeSlot)
        .HasForeignKey(session => session.TimeSlotId);
      sessionBuilder.NavigationProperty(session => session.Track)
        .HasForeignKey(session => session.TrackId);
      sessionBuilder.NavigationProperty(session => session.Speaker)
        .HasForeignKey(session => session.SpeakerId)
        .HasInverse(speaker => speaker.SpeakerSessions);

      var roomBuilder = new EntityTypeBuilder<Room>();
      roomBuilder.DataProperty(room => room.Id).IsPartOfKey().IsAutoIncrementing();

      var timeSlotBuilder = new EntityTypeBuilder<TimeSlot>();
      timeSlotBuilder.DataProperty(timeSlot => timeSlot.Id).IsPartOfKey().IsAutoIncrementing();

      var trackBuilder = new EntityTypeBuilder<Track>();
      timeSlotBuilder.DataProperty(track => track.Id).IsPartOfKey().IsAutoIncrementing();

      // Configure constraints
      personBuilder.DataProperty(person => person.FirstName).IsRequired().MaxLength(50);
      personBuilder.DataProperty(person => person.LastName).IsRequired().MaxLength(50);


      sessionBuilder.DataProperty(session => session.TrackId).IsRequired();
      sessionBuilder.DataProperty(session => session.RoomId).IsRequired();
      sessionBuilder.DataProperty(session => session.SpeakerId).IsRequired();
      sessionBuilder.DataProperty(session => session.RoomId).IsRequired();

      sessionBuilder.DataProperty(session => session.Title).IsRequired().MaxLength(50);
      sessionBuilder.DataProperty(session => session.Description).MaxLength(4000);



    }
  }
}


