﻿using System;
using OpenTK.Audio.OpenAL;
using rpvoicechat;
using OpenTK.Audio;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

public class PlayerAudioSource : IDisposable
{
    public const int BufferCount = 4;

    private readonly int source;
    private bool isPlaying = false;

    private CircularAudioBuffer buffer;
    //private ReverbEffect reverbEffect;

    private ICoreClientAPI capi;
    private Vec3f lastPos;
    private long gameTickId;
    public bool IsMuffled { get; set; } = false;
    public bool IsReverberated { get; set; } = false;

    public bool IsLocational { get; set; } = true;

    private IPlayer player;

    public PlayerAudioSource(IPlayer player, AudioOutputManager manager, ICoreClientAPI capi)
    {
        this.player = player;
        this.capi = capi;
        gameTickId = capi.Event.RegisterGameTickListener(UpdatePlayer, 20);

        lastPos = player.Entity.SidedPos.XYZFloat;

        source = AL.GenSource();
        Util.CheckError("Error gen source", capi);
        buffer = new CircularAudioBuffer(source, BufferCount, capi);

        AL.Source(source, ALSourceb.Looping, false);
        Util.CheckError("Error setting source looping", capi);
        AL.Source(source, ALSourceb.SourceRelative, false);
        Util.CheckError("Error setting source SourceRelative", capi);
        AL.Source(source, ALSourcef.Gain, 1.0f);
        Util.CheckError("Error setting source Gain", capi);
        AL.Source(source, ALSourcef.Pitch, 1.0f);
        Util.CheckError("Error setting source Pitch", capi);

        //reverbEffect = new ReverbEffect(manager.EffectsExtension, source);
    }

    public void UpdatePlayer(float dt)
    {
        if (IsMuffled)
        {
        }

        if (IsReverberated)
        {
        }

        if (IsLocational)
        {
            var entityPos = player.Entity.SidedPos.XYZFloat;
            var direction = (entityPos - capi.World.Player.Entity.SidedPos.XYZFloat);
            direction.Normalize();

            var velocity = (lastPos - entityPos) / dt;
            lastPos = entityPos;

            AL.Source(source, ALSource3f.Position, entityPos.X, entityPos.Y, entityPos.Z);
            Util.CheckError("Error setting source pos", capi);

            AL.Source(source, ALSource3f.Direction, direction.X, direction.Y, direction.Z);
            Util.CheckError("Error setting source direction", capi);

            AL.Source(source, ALSource3f.Velocity, velocity.X, velocity.Y, velocity.Z);
            Util.CheckError("Error setting source velocity", capi);

        }
        else
        {
            AL.GetListener(ALListener3f.Position, out var Pos);
            Util.CheckError("Error getting listener pos", capi);
            AL.Source(source, ALSource3f.Position, ref Pos);
            Util.CheckError("Error setting source direction", capi);
            AL.Source(source, ALSource3f.Velocity, 0, 0, 0);
            Util.CheckError("Error setting source velocity", capi);
        }
    }

    public void StartTick()
    {
        if(gameTickId != 0)
            return;

        gameTickId = capi.Event.RegisterGameTickListener(UpdatePlayer, 100);
    }

    public void StopTick()
    {
        if(gameTickId == 0)
            return;

        capi.Event.UnregisterGameTickListener(gameTickId);
    }

    public void QueueAudio(byte[] audioBytes, int bufferLength)
    {
        if(!isPlaying)
            return;

        capi.Event.EnqueueMainThreadTask(() =>
        {
            buffer.TryDequeBuffers();
            buffer.QueueAudio(audioBytes, bufferLength, ALFormat.Mono16, MicrophoneManager.Frequency);

            var state = AL.GetSourceState(source);
            Util.CheckError("Error getting source state", capi);
            // the source can stop playing if it finishes everything in queue
            if (state != ALSourceState.Playing && isPlaying)
            {
                StartPlaying();
            }
        }, "QueueAudio");
    }

    public void StartPlaying()
    {
        StartTick();
        isPlaying = true;
        AL.SourcePlay(source);
        Util.CheckError("Error playing source", capi);
    }

    public void StopPlaying()
    {
        StopTick();
        isPlaying = false;
        AL.SourceStop(source);
        Util.CheckError("Error stop playing source", capi);
    }

    public void Dispose()
    {
        AL.SourceStop(source);
        Util.CheckError("Error stop playing source", capi);

        buffer.Dispose();
        AL.DeleteSource(source);
        Util.CheckError("Error deleting source", capi);

        StopTick();
    }
}