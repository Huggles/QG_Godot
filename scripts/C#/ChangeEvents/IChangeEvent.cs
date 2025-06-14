using Godot;
using System;
using System.Threading.Tasks;

public interface IChangeEvent
{
    Task<bool> ApplyChange();
}
