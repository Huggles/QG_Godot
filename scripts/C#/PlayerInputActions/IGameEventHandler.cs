using Godot;
using System;
using System.Threading.Tasks;

public interface IGameEventHandler<T>
{
    Task<T> Handle();
}
