using System;
using System.Collections.Generic;
using System.Collections;

namespace MIPSSimulator
{
    partial class Program
    {
        static List<string> ThreeRegList = new List<string> { "AND", "OR", "XOR", "NOR", "ADD", "ADD", "ADDU", "SUB", "SUBU", "SLT" };
        static List<string> TwoRegAndImmediateList = new List<string> { "ADDI", "ADDIU", "SLTI" };
        static List<string> OneRegAndImmediateList = new List<string> { "BGTZ", "BGEZ", "BLTZ", "BLEZ" };
        static List<string> TwoRegAndShamtList = new List<string> { "SLL", "SRL", "SRA" };
        static List<string> BranchList = new List<string> { "BGTZ", "BGEZ", "BLTZ", "BLEZ", "BNE", "BEQ"};

        //Convert list of bits into its integer representation, unsignged
        static uint BitsToUInt(List<bool> bits)
        {
            uint sum = 0;
            uint exponent = 1;
            for (int i = bits.Count - 1; i >= 0; i--) //Go through each bit in the bit list, backwards, so starting at LSB
            {
                //If this bit is 1, add appropriate value to sum
                //Ex. if it's the LSB add 1, if it's the 3rd bit from the right add 4, etc.
                if (bits[i])
                {
                    sum += exponent;
                }
                exponent *= 2; //Multiply exponent by 2 so each bit has appropriate value
            }
            return sum;
        }

        //Convert list of bits into its integer representation, with 2's complement
        static int BitsToInt(List<bool> bits)
        {
            int sum = 0;
            int exponent = 1;
            for (int i = bits.Count - 1; i >= 0; i--) //Go through each bit in the bit list, backwards, so starting at LSB
            {
                //If this bit is 1, add appropriate value to sum
                //Ex. if it's the LSB add 1, if it's the 3rd bit from the right add 4, etc.
                //If the MSB is 1, then subtract exponent rather than add to get the 2's complement integer
                if (bits[i])
                {
                    if (i == 0)
                    {
                        sum -= exponent;
                    }
                    else
                    {
                        sum += exponent;
                    }
                }
                exponent *= 2; //Multiply exponent by 2 so each bit has appropriate value
            }
            return sum;
        }

        //Convert an int to its bits
        static List<bool> IntToBits(int value)
        {
            BitArray ba = new BitArray(new int[] { value });
            bool[] arr = new bool[ba.Length];
            ba.CopyTo(arr, 0);
            return new List<bool>(arr);
        }

        static Instruction DecodeInstruction(List<bool> instruction, bool stall)
        {
            if(stall)
            {
                Instruction stallinstr = new Instruction("**STALL**");
                return stallinstr;
            }
            Instruction instr = new Instruction();
            instr.pc = ctx.PC - 8;
            uint opcode = BitsToUInt(instruction.GetRange(0, 6)); //Get the 6-bit opcode from the start of the instruction and calculate its value
            //First, determine operation and instruction type
            instr.itype = InstructionType.I; //Assume I type initially, if R or J it will be overwritten
            switch (opcode)
            {
                case 0x00: //R-type, inspect funct field for further info
                    instr.itype = InstructionType.R;
                    List<bool> functbits = instruction.GetRange(26, 6);
                    uint funct = BitsToUInt(functbits);
                    switch (funct)
                    {
                        case 0x00: //SLL (also NOP, since NOP is just a SLL instr with 0 value instr.operands)
                            if (BitsToUInt(instruction) == 0) //Indicates a NOP
                            {
                                instr.op = "NOP";
                            }
                            else //Indicates a SLL
                            {
                                instr.op = "SLL";
                            }
                            break;
                        case 0x02: //SRL
                            instr.op = "SRL";
                            break;
                        case 0x03: //SRA
                            instr.op = "SRA";
                            break;
                        case 0x08: //JR
                            instr.op = "JR";
                            break;
                        case 0x0D: //BREAK
                            instr.op = "BREAK";
                            break;
                        case 0x20: //ADD
                            instr.op = "ADD";
                            break;
                        case 0x21: //ADDU
                            instr.op = "ADDU";
                            break;
                        case 0x22: //SUB
                            instr.op = "SUB";
                            break;
                        case 0x23: //SUBU
                            instr.op = "SUBU";
                            break;
                        case 0x24: //AND
                            instr.op = "AND";
                            break;
                        case 0x25: //OR
                            instr.op = "OR";
                            break;
                        case 0x26: //XOR
                            instr.op = "XOR";
                            break;
                        case 0x27: //NOR
                            instr.op = "NOR";
                            break;
                        case 0x2A: //SLT
                            instr.op = "SLT";
                            break;
                    }
                    break;
                case 0x01: //BGEZ or BLTZ
                    if (!instruction[15]) //BLTZ
                    {
                        instr.op = "BLTZ";
                    }
                    else //BGEZ
                    {
                        instr.op = "BGEZ";
                    }
                    break;
                case 0x02: //J
                    instr.itype = InstructionType.J;
                    instr.op = "J";
                    break;
                case 0x04: //BEQ
                    instr.op = "BEQ";
                    break;
                case 0x05: //BNE
                    instr.op = "BNE";
                    break;
                case 0x06: //BLEZ
                    instr.op = "BLEZ";
                    break;
                case 0x07: //BGTZ
                    instr.op = "BGTZ";
                    break;
                case 0x08: //ADDI
                    instr.op = "ADDI";
                    break;
                case 0x09: //ADDIU
                    instr.op = "ADDIU";
                    break;
                case 0x0A: //SLTI
                    instr.op = "SLTI";
                    break;
                case 0x23: //LW
                    instr.op = "LW";
                    break;
                case 0x2B: //SW
                    instr.op = "SW";
                    break;
            }
            //Next, determine important values from the instruction bits, and construct string to display instruction info
            //All register numbers are unsigned.
            if (ThreeRegList.Contains(instr.op)) //Display three 5-bit register numbers
            {
                instr.rd = BitsToUInt(instruction.GetRange(16, 5));
                instr.rs = BitsToUInt(instruction.GetRange(6, 5));
                instr.rt = BitsToUInt(instruction.GetRange(11, 5));
                instr.opstring = "R" + instr.rd + ", R" + instr.rs + ", R" + instr.rt;
                instr.read = ReadFrom.RS_RT;
                instr.write = WriteTo.RD;
            }
            else if (TwoRegAndImmediateList.Contains(instr.op)) //Display two 5-bit register numbers and a 16-bit signed immediate
            {
                instr.rt = BitsToUInt(instruction.GetRange(11, 5));
                instr.rs = BitsToUInt(instruction.GetRange(6, 5));
                instr.imm = BitsToInt(instruction.GetRange(16, 16));
                instr.opstring = "R" + instr.rt + ", R" + instr.rs + ", #" + instr.imm;
                instr.read = ReadFrom.RS;
                instr.write = WriteTo.RT;
            }
            else if (OneRegAndImmediateList.Contains(instr.op)) //Display one 5-bit register number and a 16-bit signed immediate
            {
                instr.rs = BitsToUInt(instruction.GetRange(6, 5));
                //These are all branch instructions, so also add two bits to shift left
                List<bool> offset = instruction.GetRange(16, 16); //Get target address
                offset.AddRange(new bool[] { false, false }); //Add two 0s to the end of the address to shift left
                instr.offset = BitsToInt(offset);
                instr.opstring = "R" + instr.rs + ", #" + instr.offset;
                instr.read = ReadFrom.RS;
                instr.write = WriteTo.NONE;
            }
            else if (TwoRegAndShamtList.Contains(instr.op)) //Display two 5-bit register numbers and a 5-bit unsigned shift amount
            {
                instr.rt = BitsToUInt(instruction.GetRange(11, 5));
                instr.rd = BitsToUInt(instruction.GetRange(16, 5));
                instr.shamt = BitsToUInt(instruction.GetRange(21, 5));
                instr.opstring = "R" + instr.rd + ", R" + instr.rt + ", #" + instr.shamt;
                instr.read = ReadFrom.RT;
                instr.write = WriteTo.RD;
            }
            else if (instr.op == "BNE" || instr.op == "BEQ") //Display two 5-bit register numbers and a 16-bit signed immediate, but in a different order from ADDI etc. Also immediate must be shifted left 2 bits
            {
                instr.rs = BitsToUInt(instruction.GetRange(6, 5));
                instr.rt = BitsToUInt(instruction.GetRange(11, 5));
                //These are all branch instructions, so also add two bits to shift left
                List<bool> offset = instruction.GetRange(16, 16); //Get target address
                offset.AddRange(new bool[] { false, false }); //Add two 0s to the end of the address to shift left
                instr.offset = BitsToInt(offset);
                instr.opstring = "R" + instr.rs + ", R" + instr.rt + ", #" + instr.offset;
                instr.read = ReadFrom.RS_RT;
                instr.write = WriteTo.NONE;
            }
            else if (instr.op == "SW" || instr.op == "LW") //Display 5-bit register number, then 16-bit signed offset(5-bit register number)
            {
                instr.rt = BitsToUInt(instruction.GetRange(11, 5));
                instr.offset = BitsToInt(instruction.GetRange(16, 16));
                instr.basereg = BitsToUInt(instruction.GetRange(6, 5));
                instr.opstring = "R" + instr.rt + ", " + instr.offset + "(R" + instr.basereg + ")";
                if (instr.op == "SW")
                {
                    instr.read = ReadFrom.BASEREG_RT;
                    instr.write = WriteTo.NONE;
                }
                else //LW
                {
                    instr.read = ReadFrom.BASEREG;
                    instr.write = WriteTo.RT;
                }
            }
            else if (instr.op == "JR") //Display single 5-bit register number
            {
                instr.rs = BitsToUInt(instruction.GetRange(5, 6));
                instr.opstring = "R" + instr.rs; //Get number of register which contains target address
                instr.read = ReadFrom.RS;
                instr.write = WriteTo.NONE;
            }
            else if (instr.op == "J") //Display single 26-bit unsigned target address, shifted left 2 bits
            {
                List<bool> address = instruction.GetRange(6, 26); //Get target address
                address.AddRange(new bool[] { false, false }); //Add two 0s to the end of the address to shift left
                instr.addr = BitsToUInt(address);
                instr.opstring = "#" + instr.addr;
                instr.read = ReadFrom.NONE;
                instr.write = WriteTo.NONE;
            }
            else //BREAK, NOP need not display anything
            {
                instr.opstring = "";
                instr.read = ReadFrom.NONE;
                instr.write = WriteTo.NONE;
            }
            return instr;
        }
    }
}
