﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Caching;
using System.Windows;
using Microsoft.VisualBasic.FileIO;

namespace WpfApp1.Classes
{
    class Schedule
    {
        // TODO - make sure all global variables are intialized when they have to be.. make it's supposed to be in a function and not the constructor
        // TODO - (time permitting) make variables that are needed private
        // TODO :: function to extrapolate juice schedule from equipment schedules
        public List<Equipment> machines;
        public List<Equipment> blendtanks;
        public List<Equipment> transferLines;
        public int numFunctions; // TODO: need to fill this in as soon as possible to use it in the rest of the code (1)
        public int numSOs;
        public bool[] uniqueTools; // a function is true if only one machine supports that functionality, false otherwise

        public List<Juice> finished;
        public List<Juice> inprogress;// this is "juices" i went through and changed all references to "juices" even in commented out sections
        public List<Juice> juices_line8;
        //public List<Juice> juices_line9; // thaw room TODO : deal with this...
        public DateTime scheduleID;


        public Schedule()
        {
            this.scheduleID = DateTime.Now;

            this.machines = new List<Equipment>();
            this.blendtanks = new List<Equipment>();
            this.transferLines = new List<Equipment>();
            this.numFunctions = 10; // TODO: need to change this (1)
            this.uniqueTools = new bool[numFunctions];
            this.finished = new List<Juice>();
            this.inprogress = new List<Juice>();
            this.juices_line8 = new List<Juice>();
            //this.juices_line9 = new List<Juice>();
        }

        //TODO: if string is empty then we should pop up an ERROR box
        public void ProcessCSV(string fileName)
        {
            List<String[]> lines = new List<string[]>();
            int row_start = 0;
            bool row_starter = false;
            int counter = 0;

            if(!fileName.Contains("csv"))
            {
                throw new SystemException("The selected file is not a csv.");
            }

            using(TextFieldParser parser = new TextFieldParser(fileName))
            {
                parser.TrimWhiteSpace = true;
                parser.Delimiters = new string[] { "," };
                parser.HasFieldsEnclosedInQuotes = true;
                while (!parser.EndOfData)
                {
                    string[] line = parser.ReadFields();
                    if(line[8].Contains("F_LINE") && !row_starter) 
                    { 
                        row_start = counter;
                        row_starter = true;
                    }
                    if(line[0] == "") {break;}
                    lines.Add(line);
                    counter++;
                }
            }

            Equipment thaw_room = new Equipment("Thaw Room", 0);
            machines.Add(thaw_room);


            int num_rows = lines.Count;

            inprogress = new List<Juice>();
            juices_line8 = new List<Juice>();

            //Get all the info for each "F_LINE" to make each juice needed
            for (int i = row_start; i < num_rows; i++)
            {
                if (lines[i][0] != "*" && lines[i][8].Contains("F_LINE"))
                {

                    string line_name = lines[i][8];
                    int line = Int32.Parse(line_name.Substring(line_name.Length - 1, 1));

                    // if it's not line 1,2,3,7, or 8, we can continue to the next line
                    if (!(line == 1 || line == 2 || line == 3 || line == 7 || line == 8))
                    {
                        continue;
                    }

                    string material = lines[i][2];

                    //Processing quantities to check if the juice is at it's ending stage
                    int quantity_juice = int.Parse(lines[i][4], NumberStyles.AllowThousands);
                    int quantity_juice_2 = int.Parse(lines[i][5], NumberStyles.AllowThousands);
                    bool no_batches = quantity_juice <= quantity_juice_2;
                    

                    string name = lines[i][3];

                    int type = name.Contains("CIP") ? -1: getJuiceType(name);
                    Console.WriteLine(name + " " + type);

                    string date = lines[i][0];
                    string seconds = lines[i][1];
                    string dateTime = date + " " + seconds;
                    DateTime fillTime = Convert.ToDateTime(dateTime);

                    bool starterFlag = quantity_juice_2 != 0;

                    Juice new_juice = new Juice(0, line, material, name, fillTime,  starterFlag, no_batches);

                    if(line == 8)
                    {
                        juices_line8.Add(new_juice);
                    } else
                    {
                        inprogress.Add(new_juice);
                    }
                    
                }
            }

            PrintAllJuices();
        }

        private int getJuiceType(String material_name)
        {
            try
            {
                SqlConnection conn = new SqlConnection();
                conn.ConnectionString = ConfigurationManager.ConnectionStrings["conn"].ConnectionString;
                conn.Open();

                SqlCommand cmd = new SqlCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "[select_JuiceId]";
                cmd.Parameters.Add("mat_name", SqlDbType.VarChar).Value = material_name;
                cmd.Connection = conn;

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);

                return Convert.ToInt32(dt.Rows[0]["juice_id"]);
            }

            catch (Exception ex)
            {
                //MessageBox.Show(ex.ToString());
            }

            return -1;
        }

        private void PrintAllJuices()
        {
            Console.WriteLine("Juices in lne 1,2,3,7:");
            for(int i=0; i<inprogress.Count; i++)
            {
                Console.WriteLine("Name: " + inprogress[i].name);
            }

            Console.WriteLine("Juices in line 8:");
            for (int i = 0; i < juices_line8.Count; i++)
            {
                Console.WriteLine("Name: " + inprogress[i].name);
            }

        }


        // TODO - add pull equipment function

        public CompareRecipe[] prepRecipes(Juice x)
        {
            CompareRecipe[] options = new CompareRecipe[x.recipes.Count];

            // make equipment choices for each recipe
            for (int i = 0; i < x.recipes.Count; i++)
            {
                List<List<Equipment>> recipecopy = SortByOptions(x.recipes[i]);
                bool[] checkoffFunc = new bool[numFunctions];
                int cntFunc = 0;
                bool[] checkoffsos = new bool[numSOs];
                int cntSOs = numSOs;

                for (int j = 0; j < numFunctions; j++)
                {
                    // all the functionalities have been covered
                    if (cntFunc == checkoffFunc.Length)
                        break;

                    // find out what function this list of equipm
                    int func = (recipecopy[j][0]).type * -1;

                    // case that the recipe doesn't need that function
                    if (recipecopy[j].Count == 1 && x.recipes[func][0] < 0) // TODO - need to fix that 0 in [0] because idk (1)
                    {
                        checkoffFunc[func] = true;
                        cntFunc++;
                        continue;
                    }

                    // case that the recipe needs the function and it isn't available
                    if (recipecopy[j].Count == 1)
                    {
                        //options[i].possible = false; // TODO : this doesn't exist (1)
                        break;
                    }

                    // pick a tool
                    int choice = -1;
                    DateTime currentStart = new DateTime(0,0,0);
                    int otherfuncs = 0;
                    int sos = 0;
                    bool otherUnique = false; // TODO : what do you want to initialize it to? (1) and all the variabels before it

                    for (int k = 1; k < recipecopy[j].Count; k++)
                    {
                        // check that equipment can connect

                        DateTime tempstart = GetStart(recipecopy[j][k], x.recipes[i]);
                        int tempfuncs = GetFuncs(recipecopy[j][k], x.recipes[i], checkoffFunc);
                        int tempsos = GetSOs(recipecopy[j][k], checkoffsos);
                        bool containsUnneededUnique = GetOtherUnique();

                        if (choice == -1)
                        {
                            choice = k;
                            currentStart = tempstart;
                            otherfuncs = tempfuncs;
                            sos = tempsos;
                            otherUnique = containsUnneededUnique;
                        }
                        else if (DateTime.Compare(tempstart, x.idealTime[i]) > 0 && DateTime.Compare(tempstart, currentStart) < 0)
                        {
                            choice = k;
                            currentStart = tempstart;
                            otherfuncs = tempfuncs;
                            sos = tempsos;
                            otherUnique = containsUnneededUnique;
                        }
                        else if (DateTime.Compare(tempstart, x.idealTime[i]) > 0)
                        {
                            continue;
                        }
                        else if (DateTime.Compare(tempstart, currentStart) == 0)
                        {
                            if (otherUnique && !containsUnneededUnique)
                            {
                                choice = k;
                                currentStart = tempstart;
                                otherfuncs = tempfuncs;
                                sos = tempsos;
                                otherUnique = containsUnneededUnique;
                            }
                            else if ((containsUnneededUnique && !otherUnique) || otherfuncs > tempfuncs)
                            {
                                continue;
                            }
                            else if (tempfuncs > otherfuncs)
                            {
                                choice = k;
                                currentStart = tempstart;
                                otherfuncs = tempfuncs;
                                sos = tempsos;
                                otherUnique = containsUnneededUnique;
                            }
                            else if (tempsos > sos)
                            {
                                choice = k;
                                currentStart = tempstart;
                                otherfuncs = tempfuncs;
                                sos = tempsos;
                                otherUnique = containsUnneededUnique;
                            }
                        }
                        else if (DateTime.Compare(tempstart, currentStart) < 0)
                        {
                            if (TimeSpan.Compare(currentStart.Subtract(x.idealTime[i]), new TimeSpan(1, 0, 0)) <= 0)
                            {
                                if (otherUnique && !containsUnneededUnique)
                                {
                                    choice = k;
                                    currentStart = tempstart;
                                    otherfuncs = tempfuncs;
                                    sos = tempsos;
                                    otherUnique = containsUnneededUnique;
                                }
                                else if ((containsUnneededUnique && !otherUnique) || otherfuncs > tempfuncs)
                                {
                                    continue;
                                }
                                else if (tempfuncs > otherfuncs)
                                {
                                    choice = k;
                                    currentStart = tempstart;
                                    otherfuncs = tempfuncs;
                                    sos = tempsos;
                                    otherUnique = containsUnneededUnique;
                                }
                                else if (tempsos > sos)
                                {
                                    choice = k;
                                    currentStart = tempstart;
                                    otherfuncs = tempfuncs;
                                    sos = tempsos;
                                    otherUnique = containsUnneededUnique;
                                }
                            }
                            else
                            {
                                choice = k;
                                currentStart = tempstart;
                                otherfuncs = tempfuncs;
                                sos = tempsos;
                                otherUnique = containsUnneededUnique;
                            }
                        }
                        else if (TimeSpan.Compare(tempstart.Subtract(x.idealTime[i]), new TimeSpan(1, 0, 0)) <= 0)
                        {
                            if (otherUnique && !containsUnneededUnique)
                            {
                                choice = k;
                                currentStart = tempstart;
                                otherfuncs = tempfuncs;
                                sos = tempsos;
                                otherUnique = containsUnneededUnique;
                            }
                            else if ((containsUnneededUnique && !otherUnique) || otherfuncs > tempfuncs)
                            {
                                continue;
                            }
                            else if (tempfuncs > otherfuncs)
                            {
                                choice = k;
                                currentStart = tempstart;
                                otherfuncs = tempfuncs;
                                sos = tempsos;
                                otherUnique = containsUnneededUnique;
                            }
                            else if (tempsos > sos)
                            {
                                choice = k;
                                currentStart = tempstart;
                                otherfuncs = tempfuncs;
                                sos = tempsos;
                                otherUnique = containsUnneededUnique;
                            }
                        }

                    }


                    // mark off the rest of the functionalities that piece supports


                }
            }

            return options;
        }

        public void GenerateNewSchedule()
        {
            SortByFillTime();

            while (inprogress.Count != 0)
            {
                if (inprogress[0].mixing)
                {
                    // you only have to acquire a transfer line

                    // update the batch counts
                    inprogress[0].neededBatches--;
                    if (inprogress[0].inline)
                        inprogress[0].slurryBatches--;

                    // either move juice to finished list or recalculate fill time
                    if (inprogress[0].neededBatches == 0)
                    {
                        finished.Add(inprogress[0]);
                        inprogress.RemoveAt(0);
                    }
                    else
                    {
                        inprogress[0].RecalculateFillTime();
                        SortByFillTime();
                    }
                }
                else
                {
                    if (inprogress[0].inline)
                    {
                        // you only need to acquire a transfer line

                        // update the batch counts
                        inprogress[0].neededBatches--;
                        inprogress[0].slurryBatches--;

                        // move to finished list or continue
                        if (inprogress[0].neededBatches == 0)
                        {
                            finished.Add(inprogress[0]);
                            inprogress.RemoveAt(0);
                        }
                        else
                        {
                            if (inprogress[0].slurryBatches == 0)
                                inprogress[0].inline = false;

                            inprogress[0].RecalculateFillTime();
                            SortByFillTime();
                        }
                    }
                    else
                    {
                        // it wouldn't make sense to do inline for a single batch
                        if (inprogress[0].neededBatches == 1)
                        {
                            // pick a batched recipe
                            // assign equipment

                            // move to finished list
                            finished.Add(inprogress[0]);
                            inprogress.RemoveAt(0);
                        }
                        else
                        {
                            // decide if you can do inline: can you finish the slurry for 2,3,4,or5 batches before the fill time?
                            // if you do end up doing inline, slurryBatches does not include the current batch


                            // finish with current juice and move on
                            inprogress[0].neededBatches--;
                            inprogress[0].RecalculateFillTime();
                            SortByFillTime();
                        }
                    }
                }
            }
        }

        // TODO - fill in function
        public void SortByFillTime()
        {
            // sorts inprogress by current filltime
            // use insertion sort because most calls will be on an already sorted list
        }

        public List<List<Equipment>> SortByOptions(List<int> x)
        {
            // takes the recipe and builds a new recipe sorted by the availability of the equipment
            List<List<Equipment>> options = new List<List<Equipment>>();
            for (int i = 0; i < numFunctions; i++)
            {
                options.Add(new List<Equipment>());
                options[i].Add(new Equipment(-1 * i));

                if (x[i] < 0)
                    continue;

                for (int j = 0; j < machines.Count; j++)
                    if (machines[j].functionalities[i])
                        options[i].Add(machines[j]);
            }

            // sort options by the length of the lists

            return options;
        }

        // TODO: fill in function
        public bool GetOtherUnique()
        {
            return false;
        }

        // TODO - fill in function
        public DateTime GetStart(Equipment tool, List<int> recipe)
        {
            DateTime start = new DateTime();
            return start;
        }

        // TODO: needs to be fixed (2)
        public TimeSpan Getlength(Equipment tool, List<int> recipe, bool[] funcsclaimed)
        {
            TimeSpan length = new TimeSpan();

            for (int i = 0; i < numFunctions; i++)
                if (tool.functionalities[i] && !funcsclaimed[i] && recipe[i] > 0)
                    length.Add(new TimeSpan(0, recipe[i], 0));

            return length; 
        }

        public int GetFuncs(Equipment tool, List<int> recipe, bool[] funcsclaimed)
        {
            int cnt = 0;
            for (int i = 0; i < numFunctions; i++)
                if (tool.functionalities[i] && !funcsclaimed[i] && recipe[i] > 0)
                    cnt++;

            return cnt;
        }

        // TODO - fill in function
        public int GetSOs(Equipment tool, bool[] sosclaimed)
        {
            return 0;
        }



    }
}