using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace Combat_Realism.Detours
{

    static class Detours_Pawn_JobTracker
    {
		internal static FieldInfo _jobsGivenThisTick;
		internal static FieldInfo _pawn;
		internal static MethodInfo _CanDoAnyJob;
        internal static MethodInfo _DetermineNextJob;
        internal static MethodInfo _StartErrorRecoveryJob;
        internal static MethodInfo _CheckLeaveJoinableLordBecauseJobIssued;

		internal static Pawn GetPawn(Pawn_JobTracker _this)
		{
			_pawn = _this.GetType().GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic);
			return (Pawn)_pawn.GetValue(_this);
		}

		// internal static FieldInfo FieldInfoJobsGivenThisTick()
		// {
		// 	return typeof(Pawn_JobTracker).;
		// }

		internal static void SetJobsGivenThisTick(Pawn_JobTracker _this, int value)
		{
			_jobsGivenThisTick = _this.GetType().GetField("jobsGivenThisTick", BindingFlags.Instance | BindingFlags.NonPublic);;
			_jobsGivenThisTick.SetValue(_this, value);
		}

        internal static int GetJobsGivenThisTick(Pawn_JobTracker _this)
		{
            _jobsGivenThisTick = _this.GetType().GetField("jobsGivenThisTick", BindingFlags.Instance | BindingFlags.NonPublic);;
			return (int)_jobsGivenThisTick.GetValue(_this);
		}

		internal static bool CanDoAnyJob(Pawn_JobTracker _this)
		{
            _CanDoAnyJob = _this.GetType().GetMethod("CanDoAnyJob", BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)_CanDoAnyJob.Invoke(_this, new object []{});
		}

		internal static ThinkResult DetermineNextJob(Pawn_JobTracker _this, out ThinkTreeDef thinkTree)
		{

            _DetermineNextJob = _this.GetType().GetMethod("DetermineNextJob", BindingFlags.Instance | BindingFlags.NonPublic);
            object[] args = new object[1]{null};
            ThinkResult retVal = (ThinkResult)_DetermineNextJob.Invoke(_this, args);
            thinkTree = (ThinkTreeDef)args[0];
            return retVal;
		}

		internal static void StartErrorRecoveryJob(Pawn_JobTracker _this, string message)
		{
            _StartErrorRecoveryJob = _this.GetType().GetMethod("StartErrorRecoveryJob", BindingFlags.Instance | BindingFlags.NonPublic);
            _StartErrorRecoveryJob.Invoke(_this, new object []{message});
		}

		internal static void CheckLeaveJoinableLordBecauseJobIssued(Pawn_JobTracker _this, ThinkResult result)
		{
            _CheckLeaveJoinableLordBecauseJobIssued = _this.GetType().GetMethod("CheckLeaveJoinableLordBecauseJobIssued", BindingFlags.Instance | BindingFlags.NonPublic);
            _CheckLeaveJoinableLordBecauseJobIssued.Invoke(_this, new object []{result});
		}

		public static MethodInfo _JobTrackerTick;
		internal static void JobTrackerTick(this Pawn_JobTracker _this)
		{
			// if (GetPawn(_this).ThingID == "Human762")
			// 	_this.debugLog = true;
			// _JobTrackerTick.Inv
			_JobTrackerTick.Invoke(_this, new object[] {} );
		}

		public static ConstructorInfo _Pawn_JobTracker;
		internal static Pawn_JobTracker Pawn_JobTracker(Pawn newPawn)
		{
			Pawn_JobTracker tracker = (Pawn_JobTracker)_Pawn_JobTracker.Invoke(new object[] { newPawn });
			if (newPawn.ThingID == "Human762")
				tracker.debugLog = true;
			return tracker;
		}

		internal static void TryFindAndStartJob(this Pawn_JobTracker _this)
		{
			Log.Message(String.Format("TryFindAndStartJob: {0}", _this.GetType()));
            Pawn _this_pawn = GetPawn(_this);

			if (_this_pawn.thinker == null)
			{
				Log.ErrorOnce(_this_pawn + " did TryFindAndStartJob but had no thinker.", 8573261);
				return;
			}
			if (_this_pawn.jobs.curJob != null)
			{
				Log.Warning(_this_pawn + " doing TryFindAndStartJob while still having job " + _this_pawn.jobs.curJob);
			}
			if (_this.debugLog)
			{
				_this.DebugLogEvent("TryFindAndStartJob");
			}
			_this_pawn.mindState.lastJobTag = JobTag.NoTag;
			if (CanDoAnyJob(_this))
			{
				if (_this.debugLog)
				{
					_this.DebugLogEvent("   CanDoAnyJob is false. Clearing queue and returning");
				}
				if (_this.jobQueue != null)
				{
					_this.jobQueue.Clear();
				}
				return;
			}
			ThinkTreeDef thinkTreeDef;
			ThinkResult result = DetermineNextJob(_this, out thinkTreeDef);
			if (_this_pawn.ThingID == "Human762")
			{
				Log.Message(String.Format("TryFindAndStartJob {0} {1} {2}", _this_pawn.ThingID, thinkTreeDef, result));
			}
			if (!Find.TickManager.Paused)
			{
                SetJobsGivenThisTick(_this, GetJobsGivenThisTick(_this) + 1);
			}
			if (GetJobsGivenThisTick(_this) > 10)
			{
				StartErrorRecoveryJob(_this, _this_pawn + " started 10 jobs in one tick. thinkResult=" + result.ToString());
				return;
			}
			if (result.IsValid)
			{
                CheckLeaveJoinableLordBecauseJobIssued(_this, result);
				ThinkNode sourceNode = result.SourceNode;
				ThinkTreeDef thinkTree = thinkTreeDef;
				_this.StartJob(result.Job, JobCondition.None, sourceNode, false, false, thinkTree);
			}
		}
    }
}
