using Godot;
using System;
using System.Threading.Tasks;

public interface IGameQueueItem
{
    Task Execute();
}
