using System;
using System.Collections.Generic;
using System.Collections;

namespace MIPSSimulator
{
    partial class Program
    {

        enum FromStage
        {
            EX,
            RF,
            DF,
            DS,
            WB,
        }

        enum ToReg
        {
            RT,
            RS,
            BASEREG
        }

        enum FromReg
        {
            RT,
            RD
        }

        static bool stall = false;

        /***Forwarding detection in ID stage***/

        //Functions to check for forwarding/dependencies
        static void CheckForwarding()
        {
            ClearDetected();
            Instruction instr = ctx.stageinstrs.ID;
            stall = false;
            ctx.cause = StallCause.None;
            //If we need to check for BASEREG
            if (instr.read == ReadFrom.BASEREG || instr.read == ReadFrom.BASEREG_RT)
            {
                CheckAllDependencies(instr, instr.basereg);
            }
            //If we need to check for RS
            if ((instr.read == ReadFrom.RS || instr.read == ReadFrom.RS_RT) && !stall)
            {
                CheckAllDependencies(instr, instr.rs);
            }
            //If we need to check instrs for RT
            if ((instr.read == ReadFrom.RT || instr.read == ReadFrom.RS_RT || instr.read == ReadFrom.BASEREG_RT) && !stall)
            {
                CheckAllDependencies(instr, instr.rt);
            }
        }

        static void CheckAllDependencies(Instruction instrto, uint reg)
        {
            if (reg == 0) return; //Ignore if R0 since that's just constant 0
            //Want to find soonest instr after this one which has a dependence, break after one detected
            if (CheckDependence(ctx.stageinstrs.RF, instrto, reg, FromStage.RF)) return;
            if (CheckDependence(ctx.stageinstrs.EX, instrto, reg, FromStage.EX)) return;
            if (CheckDependence(ctx.stageinstrs.DF, instrto, reg, FromStage.DF)) return;
            if (CheckDependence(ctx.stageinstrs.DS, instrto, reg, FromStage.DS)) return;
            if (CheckDependence(ctx.stageinstrs.WB, instrto, reg, FromStage.WB)) return;
        }

        static bool CheckDependence(Instruction instrfrom, Instruction instrto, uint reg, FromStage fromstage)
        {
            //If writes to RD, want to check for dependence
            if (instrfrom.write == WriteTo.RD && instrfrom.rd == reg)
            {
                AddDependence(instrfrom, instrto, fromstage);
                return true;
            }
            //Similarly check for dependence in RT
            else if (instrfrom.write == WriteTo.RT && instrfrom.rt == reg)
            {
                AddDependence(instrfrom, instrto, fromstage);
                return true;
            }
            return false;
        }

        static void AddDependence(Instruction instrfrom, Instruction instrto, FromStage fromstage)
        {
            if (instrfrom.op == "LW" && fromstage == FromStage.RF) //LW dependence 1 cycle ahead, stall for 2 cycles
            {
                ctx.stallinstr = instrto;
                ctx.stallcnt = 2;
                ctx.cause = StallCause.Load;
                ClearDetected();
                stall = true;
            }
            else if (instrfrom.op == "LW" && fromstage == FromStage.RF) //LW dependence 2 cycles ahead, stall for 1 cycle
            {
                ctx.stallinstr = instrto;
                ctx.stallcnt = 1;
                ctx.cause = StallCause.Load;
                ClearDetected();
                stall = true;
            }
            else //Normal dependency
            {
                if (ctx.forwarded.From_Detected1.op == "NOP") //First detected is empty, add there 
                {
                    ctx.forwarded.From_Detected1 = instrfrom;
                    ctx.forwarded.To_Detected1 = instrto;
                    return;
                }
                //First isn't empty, add in second slot
                ctx.forwarded.From_Detected2 = instrfrom;
                ctx.forwarded.To_Detected2 = instrto;
            }
        }

        static void ClearDetected()
        {
            //Initialize detected to nops
            ctx.forwarded.From_Detected1 = new Instruction();
            ctx.forwarded.To_Detected1 = new Instruction();
            ctx.forwarded.From_Detected2 = new Instruction();
            ctx.forwarded.To_Detected2 = new Instruction();
        }

        /***Forwarding detection during pipeline to EX stage***/

        //Functions to check for forwarding/dependencies
        static void CheckExForwarding()
        {
            //Initialize forwardings to nops
            ctx.forwarded.From_EXDF_RFEX = new Instruction();
            ctx.forwarded.From_DFDS_RFEX = new Instruction();
            ctx.forwarded.From_DSWB_RFEX= new Instruction();
            ctx.forwarded.To_EXDF_RFEX = new Instruction();
            ctx.forwarded.To_DFDS_RFEX = new Instruction();
            ctx.forwarded.To_DSWB_RFEX = new Instruction();
            ctx.forwardvalues.Use_DF_To_EX_Left = false;
            ctx.forwardvalues.Use_DF_To_EX_Right = false;
            ctx.forwardvalues.Use_DS_To_EX_Left = false;
            ctx.forwardvalues.Use_DS_To_EX_Right = false;
            ctx.forwardvalues.Use_WB_To_EX_Left = false;
            ctx.forwardvalues.Use_WB_To_EX_Right = false;
            Instruction instr = ctx.stageinstrs.EX;
            //If we need to check for BASEREG
            if (instr.read == ReadFrom.BASEREG || instr.read == ReadFrom.BASEREG_RT)
            {
                CheckAllExDependencies(instr, instr.basereg, ToReg.BASEREG);
            }
            //If we need to check for RS
            if (instr.read == ReadFrom.RS || instr.read == ReadFrom.RS_RT)
            {
                CheckAllExDependencies(instr, instr.rs, ToReg.RS);
            }
            //If we need to check instrs for RT
            if ((instr.read == ReadFrom.RT || instr.read == ReadFrom.RS_RT || instr.read == ReadFrom.BASEREG_RT) && instr.op != "SW") //Don't care about SW RT vaue in EX stage
            {
                CheckAllExDependencies(instr, instr.rt, ToReg.RT);
            }
        }

        static void CheckAllExDependencies(Instruction instrto, uint reg, ToReg toreg)
        {
            if (reg == 0) return; //Ignore if R0 since that's just constant 0
            //Want to find soonest instr after this one which has a dependence, break after one detected
            if (CheckExDependence(ctx.stageinstrs.DF, instrto, reg, FromStage.DF, toreg)) return;
            if (CheckExDependence(ctx.stageinstrs.DS, instrto, reg, FromStage.DS, toreg)) return;
            if (CheckExDependence(ctx.stageinstrs.WB, instrto, reg, FromStage.WB, toreg)) return;
        }

        static bool CheckExDependence(Instruction instrfrom, Instruction instrto, uint reg, FromStage from, ToReg toreg)
        {
            //If writes to RD, want to check for dependence
            if (instrfrom.write == WriteTo.RD && instrfrom.rd == reg)
            {
                AddExDependence(instrfrom, instrto, from, toreg, FromReg.RD);
                return true;
            }
            //Similarly check for dependence in RT
            else if (instrfrom.write == WriteTo.RT && instrfrom.rt == reg)
            {
                AddExDependence(instrfrom, instrto, from, toreg, FromReg.RT);
                return true;
            }
            return false;
        }

        static void AddExDependence(Instruction instrfrom, Instruction instrto, FromStage from, ToReg toreg, FromReg fromreg)
        {
            if (from == FromStage.DF)
            {
                ctx.forwarded.From_EXDF_RFEX = instrfrom;
                ctx.forwarded.To_EXDF_RFEX = instrto;
                ctx.forwards.EXDF_RFEX++;

                if (toreg == ToReg.RS || toreg == ToReg.BASEREG) //On the left of ALU
                {
                    ctx.forwardvalues.DF_To_EX_Left_Value = ctx.DF_DS;
                    ctx.forwardvalues.Use_DF_To_EX_Left = true;
                }
                else //toreg == ToReg.RT, on the right of ALU
                {
                    ctx.forwardvalues.DF_To_EX_Right_Value = ctx.DF_DS;
                    ctx.forwardvalues.Use_DF_To_EX_Right = true;
                }
            }
            else if (from == FromStage.DS)
            {
                ctx.forwarded.From_DFDS_RFEX = instrfrom;
                ctx.forwarded.To_DFDS_RFEX = instrto;
                ctx.forwards.DFDS_RFEX++;

                if (toreg == ToReg.RS || toreg == ToReg.BASEREG) //On the left of ALU
                {
                    ctx.forwardvalues.DS_To_EX_Left_Value = ctx.pipelineregs.DS__WB_ALUout_LMD;
                    ctx.forwardvalues.Use_DS_To_EX_Left = true;
                }
                else //toreg == ToReg.RT, on the right of ALU
                {
                    ctx.forwardvalues.DS_To_EX_Right_Value = ctx.pipelineregs.DS__WB_ALUout_LMD;
                    ctx.forwardvalues.Use_DS_To_EX_Right = true;
                }
            }
            else //from == FromStage.WB
            {
                ctx.forwarded.From_DSWB_RFEX = instrfrom;
                ctx.forwarded.To_DSWB_RFEX = instrto;
                ctx.forwards.DSWB_RFEX++;

                if (toreg == ToReg.RS || toreg == ToReg.BASEREG) //On the left of ALU
                {
                    ctx.forwardvalues.WB_To_EX_Left_Value = fromreg == FromReg.RD ? ctx.registers[(int)instrfrom.rd] : ctx.registers[(int)instrfrom.rt];
                    ctx.forwardvalues.Use_WB_To_EX_Left = true;
                }
                else //toreg == ToReg.RT, on the right of ALU
                {
                    ctx.forwardvalues.WB_To_EX_Right_Value = fromreg == FromReg.RD ? ctx.registers[(int)instrfrom.rd] : ctx.registers[(int)instrfrom.rt];
                    ctx.forwardvalues.Use_WB_To_EX_Right = true;
                }
            }
        }

        /***Forwarding detection during pipeline to DF stage***/
        //This one is easier because the only instr that needs forwarding to this stage is sw, which needs forwarding only to RT

        //Functions to check for forwarding/dependencies
        static void CheckDfForwarding()
        {
            //Initialize forwardings to nops
            ctx.forwarded.From_DFDS_EXDF = new Instruction();
            ctx.forwarded.From_DSWB_EXDF = new Instruction();
            ctx.forwarded.To_DFDS_EXDF = new Instruction();
            ctx.forwarded.To_DSWB_EXDF = new Instruction();
            ctx.forwardvalues.Use_DS_To_DF_RT = false;
            ctx.forwardvalues.Use_WB_To_DF_RT = false;
            Instruction instr = ctx.stageinstrs.DF;
            if (instr.op == "SW")
            {
                CheckAllDfDependencies(instr, instr.rt);
            }
        }

        static void CheckAllDfDependencies(Instruction instrto, uint reg)
        {
            if (reg == 0) return; //Ignore if R0 since that's just constant 0
            //Want to find soonest instr after this one which has a dependence, break after one detected
            if (CheckDfDependence(ctx.stageinstrs.DS, instrto, reg, FromStage.DS)) return;
            if (CheckDfDependence(ctx.stageinstrs.WB, instrto, reg, FromStage.WB)) return;
        }

        static bool CheckDfDependence(Instruction instrfrom, Instruction instrto, uint reg, FromStage from)
        {
            //If writes to RD, want to check for dependence
            if (instrfrom.write == WriteTo.RD && instrfrom.rd == reg)
            {
                AddDfDependence(instrfrom, instrto, from, FromReg.RD);
                return true;
            }
            //Similarly check for dependence in RT
            else if (instrfrom.write == WriteTo.RT && instrfrom.rt == reg)
            {
                AddDfDependence(instrfrom, instrto, from, FromReg.RT);
                return true;
            }
            return false;
        }

        static void AddDfDependence(Instruction instrfrom, Instruction instrto, FromStage from, FromReg fromreg)
        {
            if (from == FromStage.DS)
            {
                ctx.forwarded.From_DFDS_EXDF = instrfrom;
                ctx.forwarded.To_DFDS_EXDF = instrto;
                ctx.forwards.DFDS_EXDF++;

                ctx.forwardvalues.DS_To_DF_RT_Value = ctx.pipelineregs.DS__WB_ALUout_LMD;
                ctx.forwardvalues.Use_DS_To_DF_RT = true;
            }
            else //from == FromStage.WB
            {
                ctx.forwarded.From_DSWB_EXDF = instrfrom;
                ctx.forwarded.To_DSWB_EXDF = instrto;
                ctx.forwards.DSWB_EXDF++;

                ctx.forwardvalues.WB_To_DF_RT_Value = fromreg == FromReg.RD ? ctx.registers[(int)instrfrom.rd] : ctx.registers[(int)instrfrom.rt];
                ctx.forwardvalues.Use_WB_To_DF_RT = true;
            }
        }

    }
}
