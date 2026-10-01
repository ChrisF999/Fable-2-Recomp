// Hand-tuned override: goto-free rewrite of FrameLimiterWait_82242628.
//
// The generated (nv) copy of this function is a faithful but goto-heavy
// transliteration of the recompiler output (fable_2_recomp.89.cpp). This
// version keeps the EXACT behavior - every register/CR/XER side effect, the
// stack-slot writes, the mftb read, and all four exit paths - but replaces
// the jump spaghetti with if/else branches and a single while loop.
//
// Original structure (addresses in parentheses):
//   * Prologue: save r29/lr, push a 144-byte frame, r31=limit (r3),
//     r30=now (r4), r29=flag (r5).
//   * Readiness check #1 (0x82242648): if (deadline - now) >= (deadline -
//     counter) return WITHOUT calling sub_82B9BEC8 (the counter has already
//     reached the deadline).
//   * Optional FastHelper_821E8D20 (0x82242660): only when flag-low-byte==0
//     AND now==deadline AND limit->13232==0.
//   * Readiness check #2 (0x82242680): same test with a fresh counter read;
//     on success return again WITHOUT sub_82B9BEC8.
//   * Wait-loop setup (0x822426A8): stash limit/flag/counter-value/two
//     pointers + an mftb snapshot onto the frame, then a final re-read.
//   * Wait loop (0x822426E0): call GpuProgressCheck (ready -> exit); else
//     re-read the counter and keep spinning while (deadline - now) <
//     (deadline - counter).
//   * Epilogue: call sub_82B9BEC8 (unless we took an early readiness exit),
//     pop the frame, restore r29/lr.
#include "fable_2_pch.h"

extern "C" void FastHelper_821E8D20(PPCContext& ctx, uint8_t* base);
extern "C" void GpuProgressCheck_82B9BF90(PPCContext& ctx, uint8_t* base);
extern "C" void __restgprlr_29(PPCContext& ctx, uint8_t* base);
extern "C" void __savegprlr_29(PPCContext& ctx, uint8_t* base);
extern "C" void sub_82B9BEC8(PPCContext& ctx, uint8_t* base);

// Non-volatile equivalents of the pch's REX_LOAD_*/REX_STORE_* macros
// (identical address math, bswap, and byte order; no volatile).
#define GV8(x)   (*(const uint8_t*)(REX_RAW_ADDR(x)))
#define GV16(x)  __builtin_bswap16(*(const uint16_t*)(REX_RAW_ADDR(x)))
#define GV32(x)  __builtin_bswap32(*(const uint32_t*)(REX_RAW_ADDR(x)))
#define GV64(x)  __builtin_bswap64(*(const uint64_t*)(REX_RAW_ADDR(x)))
#define SV8(x, y)   (*(uint8_t*)(REX_RAW_ADDR(x)) = (y))
#define SV16(x, y)  (*(uint16_t*)(REX_RAW_ADDR(x)) = __builtin_bswap16(y))
#define SV32(x, y)  (*(uint32_t*)(REX_RAW_ADDR(x)) = __builtin_bswap32(y))
#define SV64(x, y)  (*(uint64_t*)(REX_RAW_ADDR(x)) = __builtin_bswap64(y))

extern "C" void FrameLimiterWait_82242628(PPCContext& __restrict ctx, uint8_t* base) {
	REX_FUNC_PROLOGUE();

	// mflr r12; bl 0x82ca2bec (save r29/lr); stwu r1,-144(r1)
	ctx.r12.u64 = ctx.lr;
	ctx.lr = 0x82242630;
	__savegprlr_29(ctx, base);
	const u32 frame_sp = ctx.r1.u32 - 144;
	SV32(frame_sp, ctx.r1.u32);
	ctx.r1.u32 = frame_sp;

	// mr r31,r3 (limit); mr r30,r4 (now); mr r29,r5 (flag)
	const u32 limit = static_cast<u32>(ctx.r3.u64);
	ctx.r31.u64 = ctx.r3.u64;
	const u32 now = static_cast<u32>(ctx.r4.u64);
	ctx.r30.u64 = ctx.r4.u64;
	ctx.r29.u64 = ctx.r5.u64;

	// Pop the frame and restore r29/lr. `call_epilogue_sub` mirrors the two
	// ways the original leaves the function: the early readiness exits jump
	// straight past sub_82B9BEC8, while the wait-loop exit (and the
	// post-mftb exit) fall through and call it first.
	auto finish = [&](bool call_epilogue_sub) {
		if (call_epilogue_sub) {
			ctx.r3.s64 = static_cast<int64_t>(frame_sp) + 80;
			ctx.lr = 0x82242714;
			sub_82B9BEC8(ctx, base);
		}
		ctx.r1.s64 = static_cast<int64_t>(frame_sp) + 144;
		__restgprlr_29(ctx, base);
	};

	// Readiness check #1 (0x82242648).
	//   r9  = deadline - now       (subf r9,r30,r10)
	//   r10 = deadline
	//   r11 = deadline - counter   (lwz r11,10896; lwz r11,0(r11); subf r11,r11,r10)
	//   bge cr6 -> 0x82242714      (exit, no sub_82B9BEC8)
	const u32 deadline  = GV32(limit + 10908);
	const u32 counter   = GV32(GV32(limit + 10896));
	const u32 now_lag   = deadline - now;         // r9
	const u32 ctr_lag   = deadline - counter;     // r11
	ctx.r10.u64 = deadline;
	ctx.r9.u64  = now_lag;
	ctx.r11.u64 = ctr_lag;
	ctx.cr6.compare<u32>(now_lag, ctr_lag, ctx.xer);
	if (!ctx.cr6.lt) {
		finish(false);
		return;
	}

	// Optional FastHelper block (0x82242660). clrlwi. r11,r6,24 tests the LOW
	// byte of r6 (the recompiler models clrlwi as keeping the low bits).
	//   r6_lowbyte != 0            -> skip helper (fall through)
	//   now != deadline            -> skip helper (fall through)
	//   limit->13232 != 0          -> exit WITHOUT sub_82B9BEC8
	//   otherwise                  -> call FastHelper_821E8D20
	const u32 r6_low = ctx.r6.u32 & 0xFF;
	ctx.r11.u64 = r6_low;
	ctx.cr0.compare<int32_t>(static_cast<int32_t>(r6_low), 0, ctx.xer);
	bool helper_gate_open = ctx.cr0.eq;
	if (helper_gate_open) {
		ctx.r11.u64 = deadline;
		ctx.cr6.compare<u32>(now, deadline, ctx.xer);
		helper_gate_open = ctx.cr6.eq;
	}
	if (helper_gate_open) {
		const u32 flag_word = GV32(limit + 13232);
		ctx.r11.u64 = flag_word;
		ctx.cr6.compare<u32>(flag_word, 0, ctx.xer);
		if (!ctx.cr6.eq) {
			finish(false);
			return;
		}
		ctx.lr = 0x82242680;
		FastHelper_821E8D20(ctx, base);
	}

	// Readiness check #2 (0x82242680), fresh counter read.
	//   r8  = counter value
	//   r9  = deadline - now
	//   r10 = deadline - counter
	//   bge cr6 -> 0x82242714      (exit, no sub_82B9BEC8)
	const u32 ctr_ptr2   = GV32(limit + 10896);
	const u32 deadline2  = GV32(limit + 10908);
	const u32 counter2   = GV32(ctr_ptr2);
	const u32 now_lag2   = deadline2 - now;
	const u32 ctr_lag2   = deadline2 - counter2;
	ctx.r8.u64  = counter2;
	ctx.r9.u64  = now_lag2;
	ctx.r10.u64 = ctr_lag2;
	ctx.cr6.compare<u32>(now_lag2, ctr_lag2, ctx.xer);
	if (!ctx.cr6.lt) {
		finish(false);
		return;
	}

	// Wait-loop setup (0x822426A8). Stash the data GpuProgressCheck reads
	// from the frame, plus an mftb snapshot.
	const u32 counter_value = GV32(ctr_ptr2);
	const u32 base_ptr      = GV32(GV32(ctx.r13.u32 + 256) + 88);
	SV32(frame_sp + 80, limit);
	SV32(frame_sp + 84, static_cast<u32>(ctx.r29.u64));
	SV32(frame_sp + 88, counter_value);
	SV32(frame_sp + 92, base_ptr);
	SV32(frame_sp + 96, base_ptr);
	const u32 tb = static_cast<u32>(REX_QUERY_TIMEBASE());  // mftb r11
	ctx.r11.u64 = tb;
	SV32(frame_sp + 100, tb);

	// Final readiness re-read (0x822426D4): if the counter has now reached the
	// deadline, fall out to the epilogue (WITH sub_82B9BEC8) without spinning.
	const u32 deadline3 = GV32(limit + 10908);
	const u32 counter3  = GV32(GV32(limit + 10896));
	const u32 now_lag3  = deadline3 - now;
	const u32 ctr_lag3  = deadline3 - counter3;
	ctx.r11.u64 = now_lag3;
	ctx.r10.u64 = ctr_lag3;
	ctx.cr6.compare<u32>(now_lag3, ctr_lag3, ctx.xer);
	bool waiting = ctx.cr6.lt;

	// Wait loop (0x822426E0).
	while (waiting) {
		ctx.r3.s64 = static_cast<int64_t>(frame_sp) + 80;
		ctx.lr = 0x822426E8;
		GpuProgressCheck_82B9BF90(ctx, base);
		ctx.cr0.compare<int32_t>(ctx.r3.s32, 0, ctx.xer);   // cmpwi r3,0
		if (ctx.cr0.eq) break;                              // ready -> exit
		// Loop exit test (0x822426F4): keep spinning while
		// (deadline - now) < (deadline - counter), i.e. counter < deadline.
		const u32 deadline4 = GV32(limit + 10908);
		const u32 counter4  = GV32(GV32(limit + 10896));
		const u32 now_lag4  = deadline4 - now;
		const u32 ctr_lag4  = deadline4 - counter4;
		ctx.r9.u64  = now_lag4;
		ctx.r11.u64 = ctr_lag4;
		ctx.cr6.compare<u32>(now_lag4, ctr_lag4, ctx.xer);
		waiting = ctx.cr6.lt;
	}

	// Wait-loop / post-mftb exit (0x8224270C): call sub_82B9BEC8, then epilogue.
	finish(true);
}
