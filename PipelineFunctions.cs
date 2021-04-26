using System;
using System.Collections.Generic;
using System.Collections;

namespace MIPSSimulator
{
    partial class Program
    {
        static void WriteBack(Instruction instr, int value)
        {
            if (instr.write == WriteTo.RD)
            {
                ctx.registers[(int)instr.rd] = value;
            }
            else if (instr.write == WriteTo.RT)
            {
                ctx.registers[(int)instr.rt] = value;
            }
        }

        static void WriteMemory(Instruction instr, int addr)
        {
            int address = (addr - 600) / 4; //Get actual address
            int value = ctx.registers[(int)instr.rt];
            if (ctx.forwardvalues.Use_DS_To_DF_RT)
            {
                value = ctx.forwardvalues.DS_To_DF_RT_Value;
            }
            else if (ctx.forwardvalues.Use_WB_To_DF_RT)
            {
                value = ctx.forwardvalues.WB_To_DF_RT_Value;
            }
            ctx.data[address] = value;
        }

        static int ReadMemory(Instruction instr, int addr)
        {
            int address = (addr - 600) / 4; //Get actual address
            return ctx.data[address];
        }

        static bool ResolveBranch(Instruction instr, int left, int right)
        {
            if (instr.op == "BNE")
            {
                return left != right;
            }
            else if (instr.op == "BEQ")
            {
                return left == right;
            }
            else if (instr.op == "BGTZ")
            {
                return left > 0;
            }
            else if (instr.op == "BGEZ")
            {
                return left >= 0;
            }
            else if (instr.op == "BLTZ")
            {
                return left < 0;
            }
            else //instr.op == BLEZ
            {
                return left <= 0;
            }
        }

        static int Execute(Instruction instr)
        {
            //First must set the operands 
            //Most will use RS and RT, which are EX.A and EX.B
            int left = ctx.pipelineregs.RF__EX_A;
            int right = ctx.pipelineregs.RF__EX_B;
            if (instr.op == "ADDU" || instr.op == "SUBU" || instr.op == "ADDIU") //Unsigned instr, convert to bits and interpret as unsigned
            {
                //Left will still be an int, but its bit representation must be interpreted as uint, so we will convert the int to bits, those bits to uint, then that uint back to an int
                left = (int)BitsToUInt(IntToBits(left));
                right = (int)BitsToUInt(IntToBits(right));
            }
            if (TwoRegAndImmediateList.Contains(instr.op)) //Want to use immediate
            {
                right = instr.imm;
            }
            else if (TwoRegAndShamtList.Contains(instr.op))
            {
                left = (int)instr.shamt;
                right = ctx.registers[(int)instr.rt];
            }
            else if (instr.op == "SW" || instr.op == "LW")
            {
                right = instr.offset;
            }
            //Now must check for forwarding which can overwrite previously set values
            if(ctx.forwardvalues.Use_DF_To_EX_Left)
            {
                left = ctx.forwardvalues.DF_To_EX_Left_Value;
            }
            else if (ctx.forwardvalues.Use_DS_To_EX_Left)
            {
                left = ctx.forwardvalues.DS_To_EX_Left_Value;
            }
            else if (ctx.forwardvalues.Use_WB_To_EX_Left)
            {
                left = ctx.forwardvalues.WB_To_EX_Left_Value;
            }
            if (ctx.forwardvalues.Use_DF_To_EX_Right)
            {
                right = ctx.forwardvalues.DF_To_EX_Right_Value;
            }
            else if (ctx.forwardvalues.Use_DS_To_EX_Right)
            {
                right = ctx.forwardvalues.DS_To_EX_Right_Value;
            }
            else if (ctx.forwardvalues.Use_WB_To_EX_Right)
            {
                right = ctx.forwardvalues.WB_To_EX_Right_Value;
            }
            //Now perform operation
            int a = left;
            int b = right;
            if (instr.op == "ADD" || instr.op == "ADDI" || instr.op == "ADDU" || instr.op == "ADDIU" || instr.op == "SW" || instr.op == "LW")
            {
                return a + b;
            }
            else if (instr.op == "SUB" || instr.op == "SUBU")
            {
                return a - b;
            }
            else if (instr.op == "AND")
            {
                return a & b;
            }
            else if (instr.op == "OR")
            {
                return a | b;
            }
            else if (instr.op == "XOR")
            {
                return a ^ b;
            }
            else if (instr.op == "NOR")
            {
                return ~(a | b);
            }
            else if (instr.op == "SLL")
            {
                return b << a;
            }
            else if (instr.op == "SRA")
            {
                return b >> a;
            }
            else if (instr.op == "SRL")
            {
                return (int)((uint)b >> a);
            }
            else if (instr.op == "SLTI" || instr.op == "SLT")
            {
                return a < b ? 1 : 0;
            }
            //Jumps and branches
            else if (instr.op == "JR" || instr.op == "J")
            {
                ctx.takebranch = true;
                return (int)instr.addr;
            }
            else if (BranchList.Contains(instr.op))
            {
                ctx.takebranch = ResolveBranch(instr, left, right);
                return instr.pc + 4 + instr.offset;
            }
            return 0;
        }
    }
}
