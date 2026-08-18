using System.Collections.Generic;

namespace MoonDelivery
{
    public interface IGameStorage
    {
        void Save(GameState state);
        GameState Load();
        void Delete();
    }

    internal interface IRandomSource
    {
        float Value { get; }
        float Range(float minimum, float maximum);
        int Range(int minimum, int maximum);
    }

    internal interface IGameContext
    {
        GameState State { get; }
        int Day { get; }
        int MinuteOfDay { get; }

        MapPoint Point(string id);
        List<MapPoint> AllMapPoints();
        RouteSegment Leg(string fromId, string toId);
        Rover Rover(string id);
        Order Order(string id);

        void Log(string message);
        void Emit(GameCue cue);
        void Save();
        void CheckGameOver();
    }
}
