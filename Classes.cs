using System;
using System.Collections.Generic;
using System.Text;

namespace MIPSSimulator
{
    enum InstructionType
    {
        I,
        R,
        J,
    }

    enum ReadFrom
    {
        RS_RT,
        RT,
        BASEREG,
        BASEREG_RT,
        RS,
        NONE
    }

    enum WriteTo
    {
        RT,
        RD,
        NONE
    }

    enum StallCause
    {
        Load,
        Branch,
        Other,
        None
    }

    class Instruction
    {
        //Will always have these
        public string op = "NOP"; //Ex. ADD
        public InstructionType itype;
        public string opstring = ""; //For displaying instr info
        public int pc;
        //Each of these may or may not be used depending on the instruction
        //Registers
        public uint rt;
        public uint rs;
        public uint rd;
        public uint basereg; //For LW and SW
        //Numbers
        public int imm;
        public int offset; //For branches and load/stores
        public uint shamt; //For shift instrs
        public uint addr; //For J
        //Info about regs read from and written to, to make forwarding easier
        public ReadFrom read;
        public WriteTo write;

        public Instruction() { }
        public Instruction(string opString)
        {
            op = opString;
        }

        public string FullOp()
        {
            return op + " " + opstring;
        }
    }

    class StageInstructions
    {
        //In the first two stages, just some bits
        public List<bool> IF = new List<bool>();
        public List<bool> IS = new List<bool>();
        //Instructions have now been decoded, so stored as instruction object
        public Instruction ID = new Instruction();
        public Instruction RF = new Instruction();
        public Instruction EX = new Instruction();
        public Instruction DF = new Instruction();
        public Instruction DS = new Instruction();
        public Instruction WB = new Instruction();

        public StageInstructions()
        {
            for(int i = 0; i < 32; i++)
            {
                IF.Add(false);
                IS.Add(false);
            }
        }
    }

    class ForwardedInstructions
    {
        public Instruction From_Detected1 = new Instruction();
        public Instruction To_Detected1 = new Instruction();
        public Instruction From_Detected2 = new Instruction();
        public Instruction To_Detected2 = new Instruction();


        public Instruction From_EXDF_RFEX = new Instruction();
        public Instruction To_EXDF_RFEX = new Instruction();

        public Instruction From_DFDS_EXDF = new Instruction();
        public Instruction To_DFDS_EXDF = new Instruction();

        public Instruction From_DFDS_RFEX = new Instruction();
        public Instruction To_DFDS_RFEX = new Instruction();

        public Instruction From_DSWB_EXDF = new Instruction();
        public Instruction To_DSWB_EXDF = new Instruction();

        public Instruction From_DSWB_RFEX = new Instruction();
        public Instruction To_DSWB_RFEX = new Instruction();
    }

    class PipelineRegisters
    {
        public int IF__IS_NPC = 0;
        public List<bool> IS__ID_IR = new List<bool>();
        public int RF__EX_A = 0;
        public int RF__EX_B = 0;
        public int EX__DF_ALUout = 0;
        public int EX__DF_B = 0;
        public int DS__WB_ALUout_LMD = 0;
    }

    class TotalStalls
    {
        public int Loads = 0;
        public int Branches = 0;
        public int Other = 0;
    }

    class TotalForwards
    {
        public int EXDF_RFEX = 0;
        public int DFDS_EXDF = 0;
        public int DFDS_RFEX = 0;
        public int DSWB_EXDF = 0;
        public int DSWB_RFEX = 0;
    }

    class ForwardValues
    {
        //EX Forwarding
        public int DF_To_EX_Left_Value = 0;
        public bool Use_DF_To_EX_Left = false;
        public int DF_To_EX_Right_Value = 0;
        public bool Use_DF_To_EX_Right = false;

        public int DS_To_EX_Left_Value = 0;
        public bool Use_DS_To_EX_Left = false;
        public int DS_To_EX_Right_Value = 0;
        public bool Use_DS_To_EX_Right = false;

        public int WB_To_EX_Left_Value = 0;
        public bool Use_WB_To_EX_Left = false;
        public int WB_To_EX_Right_Value = 0;
        public bool Use_WB_To_EX_Right = false;


        //DF Forwarding (Forward to SW RT value)
        public int DS_To_DF_RT_Value = 0;
        public bool Use_DS_To_DF_RT = false;
        public int WB_To_DF_RT_Value = 0;
        public bool Use_WB_To_DF_RT = false;
    }

    class ExecutionContext
    {
        //General info
        public int PC;
        public int cycle = 0;   
        public List<int> registers = new List<int>();
        public List<int> data = new List<int>();
        public int breakPC = -1; //PC when BREAK instruction is encountered

        //Stage information
        public StageInstructions stageinstrs = new StageInstructions();
        public PipelineRegisters pipelineregs = new PipelineRegisters();
        public int DF_DS = 0;

        //Stuff for forwarding
        public ForwardedInstructions forwarded = new ForwardedInstructions();
        public TotalForwards forwards = new TotalForwards();
        public ForwardValues forwardvalues = new ForwardValues();

        //Stuff for stalls
        public Instruction stallinstr = new Instruction("(none)");
        public StallCause cause = StallCause.None;
        public int stallcnt = 0;
        public TotalStalls stalls = new TotalStalls();

        public bool takebranch = false;

        public ExecutionContext(int StartAddr)
        {
            PC = StartAddr;
            for(int i = 0; i < 32; i++)
            {
                if(i < 100)
                {
                    data.Add(0);
                }
                registers.Add(0);
                pipelineregs.IS__ID_IR.Add(false);
            }
        }

        //Stall from a branch
        public void BranchStall()
        {
            PC = pipelineregs.EX__DF_ALUout;
            pipelineregs.EX__DF_B = 0;
            pipelineregs.RF__EX_A = 0;
            pipelineregs.RF__EX_B = 0;
            stageinstrs.EX = new Instruction("**STALL**");
            stageinstrs.RF = new Instruction("**STALL**");
            stageinstrs.ID = new Instruction("**STALL**");
            stageinstrs.IS = new List<bool>();
            for (int i = 0; i < 32; i++)
            {
                stageinstrs.IS.Add(false);
            }
            stalls.Branches += 4;
        }
    }
}
