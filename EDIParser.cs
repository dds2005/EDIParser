using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EDIParser
{
    public class EDIParser
    {

        public EDI852 Parse825(string FilePath)
        {
            StreamReader reader = new StreamReader(FilePath);
            String message = reader.ReadToEnd();

            // Discover the delimiters used. They're always in the same positions 
            char SegDelimiter = message[105];
            char ElemDelimiter = message[103];

            List<Segment> segments = (from seg in message.Split(SegDelimiter).Select(x => x.Trim())
                                      where !String.IsNullOrEmpty(seg)
                                      select new Segment
                                      {
                                          SegID = seg.Substring(0, seg.IndexOf(ElemDelimiter)),
                                          Elements = seg.Split(ElemDelimiter).Skip(1).ToArray()
                                      }).ToList();

            EDI852 docReference = new EDI852();
            EDI852 docResult = new EDI852();
            Process(docReference, docResult, segments);

            return docResult;
        }

        private void Process(EDI852 DocReference, EDI852 DocResult, List<Segment> Segments)
        {
            int iCurrentSegment = 0;

            foreach (EDISegment eDISegment in DocReference.Segments)
            {
                if (eDISegment is EDILoop)
                {
                    bool bFirst = true;

                    EDISegment eDISegmentResult = DocResult.Segments.Find(c => c.ID == eDISegment.ID);

                    while (iCurrentSegment < Segments.Count && ((EDILoop)eDISegment).Children.First().ID != Segments[iCurrentSegment].SegID)
                        iCurrentSegment++;
                    if (iCurrentSegment >= Segments.Count)
                        return;

                    while (((EDILoop)eDISegment).Children.First().ID == Segments[iCurrentSegment].SegID)
                    {
                        if ((iCurrentSegment + 1) < Segments.Count && Segments[iCurrentSegment + 1].SegID != ((EDILoop)eDISegment).Children[1].ID)
                            break;

                        if (bFirst)
                            bFirst = false;
                        else
                        {
                            eDISegmentResult = new EDILoop() { ID = eDISegment.ID, Name = eDISegment.Name, SegmentUse = eDISegment.SegmentUse };
                            foreach (EDIElement elem in eDISegment.Elements)
                                eDISegmentResult.Elements.Add(new EDIElement() { ID = elem.ID, Name = elem.Name, Position = elem.Position });
                            foreach (EDISegment segm in ((EDILoop)eDISegment).Children)
                            {
                                EDISegment segmResult = null;

                                if (segm is EDILoop)
                                {
                                    segmResult = new EDILoop() { ID = segm.ID, Name = segm.Name };
                                    foreach (EDISegment segm1 in ((EDILoop)segm).Children)
                                    {
                                        EDISegment segmResult1 = new EDISegment() { ID = segm1.ID, Name = segm1.Name };
                                        foreach (EDIElement elem1 in segm1.Elements)
                                            segmResult1.Elements.Add(new EDIElement() { ID = elem1.ID, Name = elem1.Name, Position = elem1.Position });

                                        ((EDILoop)segmResult).Children.Add(segmResult1);
                                    }
                                }
                                else
                                    segmResult = new EDISegment() { ID = segm.ID, Name = segm.Name };

                                foreach (EDIElement elemResult in segm.Elements)
                                    segmResult.Elements.Add(new EDIElement() { ID = elemResult.ID, Name = elemResult.Name, Position = elemResult.Position });


                                ((EDILoop)eDISegmentResult).Children.Add(segmResult);
                            }
                            DocResult.Segments.Add(eDISegmentResult);
                        }

                        foreach (EDISegment child in ((EDILoop)eDISegment).Children)
                        {
                            if (child is EDILoop)
                            {
                                bool bFirstChild = true;

                                EDISegment eDISegmentResultLoop = ((EDILoop)eDISegmentResult).Children.Find(c => c.ID == child.ID);

                                while (iCurrentSegment < Segments.Count && ((EDILoop)eDISegmentResultLoop).Children.First().ID != Segments[iCurrentSegment].SegID)
                                    iCurrentSegment++;
                                if (iCurrentSegment >= Segments.Count)
                                    return;

                                while (((EDILoop)eDISegmentResultLoop).Children.First().ID == Segments[iCurrentSegment].SegID)
                                {
                                    //if ((iCurrentSegment + 1) < Segments.Count && Segments[iCurrentSegment + 1].SegID != ((EDILoop)eDISegmentResultLoop).Children[1].ID)
                                    //    break;

                                    if (bFirstChild)
                                        bFirstChild = false;
                                    else
                                    {
                                        eDISegmentResultLoop = new EDILoop() { ID = child.ID, Name = child.Name, SegmentUse = child.SegmentUse };
                                        foreach (EDIElement elem in child.Elements)
                                            eDISegmentResultLoop.Elements.Add(new EDIElement() { ID = elem.ID, Name = elem.Name, Position = elem.Position });
                                        foreach (EDISegment segm in ((EDILoop)child).Children)
                                        {
                                            EDISegment segmResult = new EDISegment() { ID = segm.ID, Name = segm.Name };
                                            foreach (EDIElement elemResult in segm.Elements)
                                                segmResult.Elements.Add(new EDIElement() { ID = elemResult.ID, Name = elemResult.Name, Position = elemResult.Position });
                                            ((EDILoop)eDISegmentResultLoop).Children.Add(segmResult);
                                        }
                                        ((EDILoop)eDISegmentResult).Children.Add(eDISegmentResultLoop);
                                    }

                                    foreach (EDISegment childLoop in ((EDILoop)eDISegmentResultLoop).Children)
                                    {
                                        ProcessSegment(Segments, childLoop, eDISegmentResultLoop, ref iCurrentSegment);

                                        if (iCurrentSegment >= Segments.Count)
                                            return;
                                    }
                                }
                            }
                            else
                                ProcessSegment(Segments, child, eDISegmentResult, ref iCurrentSegment);

                            if (iCurrentSegment >= Segments.Count)
                                return;
                        }
                    }
                }
                else
                {
                    while (iCurrentSegment < Segments.Count && eDISegment.ID != Segments[iCurrentSegment].SegID)
                        iCurrentSegment++;
                    if (iCurrentSegment >= Segments.Count)
                        return;

                    if (eDISegment.ID == Segments[iCurrentSegment].SegID)
                    {
                        EDISegment eDISegmentResult = DocResult.Segments.Find(c => c.ID == eDISegment.ID);
                        for (int i = 0; i < Segments[iCurrentSegment].Elements.Count(); i++)
                            eDISegmentResult.Elements[i].Value = Segments[iCurrentSegment].Elements[i];
                    }
                    else
                    {
                        if (iCurrentSegment < Segments.Count)
                            iCurrentSegment++;
                        else
                            return;
                    }
                }
            }
        }

        private void ProcessSegment(List<Segment> Segments, EDISegment child, EDISegment eDISegmentResult, ref int iCurrentSegment)
        {
            while (iCurrentSegment < Segments.Count && child.ID != Segments[iCurrentSegment].SegID)
                iCurrentSegment++;
            if (iCurrentSegment >= Segments.Count)
                return;

            if (child.SegmentUse == EEDISegmentUse.Single)
            {
                if (child.ID == Segments[iCurrentSegment].SegID)
                {
                    EDISegment childResult = ((EDILoop)eDISegmentResult).Children.Find(c => c.ID == child.ID);
                    for (int i = 0; i < Segments[iCurrentSegment].Elements.Count(); i++)
                        childResult.Elements[i].Value = Segments[iCurrentSegment].Elements[i];

                    if (iCurrentSegment < Segments.Count)
                        iCurrentSegment++;
                    else
                        return;

                    return; // continue;
                }
            }
            else if (child.SegmentUse == EEDISegmentUse.Multiple)
            {

                bool bFirst = true;
                while (child.ID == Segments[iCurrentSegment].SegID)
                {
                    EDISegment childResult = null;
                    if (bFirst)
                        childResult = ((EDILoop)eDISegmentResult).Children.Find(c => c.ID == child.ID);
                    else
                    {
                        childResult = new EDISegment() { ID = child.ID, Name = child.Name, SegmentUse = child.SegmentUse };
                        foreach (EDIElement elem in child.Elements)
                            childResult.Elements.Add(new EDIElement() { ID = elem.ID, Name = elem.Name, Position = elem.Position });
                    }
                    for (int i = 0; i < Segments[iCurrentSegment].Elements.Count(); i++)
                        childResult.Elements[i].Value = Segments[iCurrentSegment].Elements[i];

                    if (bFirst)
                        bFirst = false;
                    else
                        ((EDILoop)eDISegmentResult).Children.Add(childResult);


                    if (iCurrentSegment < Segments.Count)
                        iCurrentSegment++;
                    else
                        return;
                }
                return; // continue;
            }
        }

        private IEnumerable<XStreamingElement> Ranks(XElement RankDefinition, IEnumerable<Segment> Segments)
        {
            if (RankDefinition.Name.LocalName == "Rank")
            {
                String BeginningSegment = RankDefinition.Elements("Segment").First().Attribute("ID").Value;
                String EndingSegment = RankDefinition.Elements("Segment").Last().Attribute("ID").Value;
                List<IEnumerable<Segment>> SegmentGroups = new List<IEnumerable<Segment>>();
                List<Segment> CurrentGroup = null;
                foreach (Segment seg in Segments)
                {
                    if (seg.SegID == BeginningSegment)
                        CurrentGroup = new List<Segment>();

                    if (CurrentGroup != null)
                        CurrentGroup.Add(seg);

                    if (seg.SegID == EndingSegment)
                    {
                        SegmentGroups.Add(CurrentGroup);
                        CurrentGroup = null;
                    }
                }

                List<XStreamingElement> _result = new List<XStreamingElement>();
                foreach (IEnumerable<Segment> g in SegmentGroups)
                {
                    foreach (XElement xElement in RankDefinition.Elements())
                    {
                        //XStreamingElement xStreamingElement = new XStreamingElement(RankDefinition.Attribute("Name").Value.Replace(' ', '_').Replace('/', '_'), Ranks(xElement, g));
                        foreach (XStreamingElement xStreamingElement in Ranks(xElement, g))
                        {
                            _result.Add(xStreamingElement);
                        }
                    }

                }
                return _result;

                //return from g in SegmentGroups
                //       select new XStreamingElement(RankDefinition.Attribute("Name").Value.Replace(' ', '_'),
                //                            from e in RankDefinition.Elements()
                //                            select Ranks(e, g));
            }

            if (RankDefinition.Name.LocalName == "Segment")
            {
                var Matching = from s in Segments
                               where s.SegID == RankDefinition.Attribute("ID").Value
                               select s;

                //List<XStreamingElement> _result = new List<XStreamingElement>();

                var _result = new XStreamingElement[] {
            new XStreamingElement(RankDefinition.Attribute("Name").Value.Replace(' ', '_').Replace('/', '_'),
                                     from s in Matching
                                     from e in RankDefinition.Elements("Element")
                                     where s.Elements.Length >= int.Parse(e.Attribute("Position").Value)
                                     select new XElement(e.Attribute("Name").Value.Replace(' ', '_').Replace('/', '_'),
                                                s.Elements[int.Parse(e.Attribute("Position").Value) - 1]))
        };
                return _result;
            }

            return null;
        }
    }

    public class Segment
    {
        public string SegID { get; set; }
        public string[] Elements { get; set; }
    }
    public enum EEDISegmentUse
    {
        Single,
        Multiple
    }
    public class EDISegment
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public List<EDIElement> Elements { get; set; }
        public EEDISegmentUse SegmentUse { get; set; }
        public EDISegment()
        {
            Elements = new List<EDIElement>();
        }
    }

    public class EDIElement
    {
        public string ID { get; set; }
        public int Position { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
    public class EDILoop : EDISegment
    {
        public List<EDISegment> Children { get; set; }
        public EDILoop()
        {
            Elements = new List<EDIElement>();
            Children = new List<EDISegment>();
        }
    }

    public class EDI852
    {
        public List<EDISegment> Segments { get; set; }
        public EDI852()
        {
            Segments = new List<EDISegment>();

            EDISegment isa = new EDISegment() { ID = "ISA", Name = "Interchange Control Header" };
            isa.Elements.Add(new EDIElement() { Position = 1, ID = "I01", Name = "Authorization Information Qualifier" });
            isa.Elements.Add(new EDIElement() { Position = 2, ID = "I01", Name = "Authorization Information" });
            isa.Elements.Add(new EDIElement() { Position = 3, ID = "I01", Name = "Security Information Qualifier" });
            isa.Elements.Add(new EDIElement() { Position = 4, ID = "I01", Name = "Security Information" });
            isa.Elements.Add(new EDIElement() { Position = 5, ID = "I01", Name = "Interchange ID Qualifier" });
            isa.Elements.Add(new EDIElement() { Position = 6, ID = "I01", Name = "Interchange Sender ID" });
            isa.Elements.Add(new EDIElement() { Position = 7, ID = "I01", Name = "Interchange ID Qualifier" });
            isa.Elements.Add(new EDIElement() { Position = 8, ID = "I01", Name = "Interchange Receiver ID" });
            isa.Elements.Add(new EDIElement() { Position = 9, ID = "I01", Name = "Interchange Date" });
            isa.Elements.Add(new EDIElement() { Position = 10, ID = "I01", Name = "Interchange Time" });
            isa.Elements.Add(new EDIElement() { Position = 11, ID = "I01", Name = "Interchange Control Standards ID" });
            isa.Elements.Add(new EDIElement() { Position = 12, ID = "I01", Name = "Interchange Control Version Num" });
            isa.Elements.Add(new EDIElement() { Position = 13, ID = "I01", Name = "Interchange Control Number" });
            isa.Elements.Add(new EDIElement() { Position = 14, ID = "I01", Name = "Acknowledgement Requested" });
            isa.Elements.Add(new EDIElement() { Position = 15, ID = "I01", Name = "Usage Indicator" });
            isa.Elements.Add(new EDIElement() { Position = 16, ID = "I01", Name = "Component Element Separator" });
            Segments.Add(isa);

            EDISegment gs = new EDISegment() { ID = "GS", Name = "Functional Group Header" };
            gs.Elements.Add(new EDIElement() { Position = 1, ID = "479", Name = "Functional Identifier Code" });
            gs.Elements.Add(new EDIElement() { Position = 2, ID = "142", Name = "Application Senders Code" });
            gs.Elements.Add(new EDIElement() { Position = 3, ID = "124", Name = "Application Receivers Code" });
            gs.Elements.Add(new EDIElement() { Position = 4, ID = "373", Name = "Date" });   // Date expressed as CCYYMMDD
            gs.Elements.Add(new EDIElement() { Position = 5, ID = "337", Name = "Time" });   //Time expressed in 24-hour clock time as follows: HHMM, or HHMMSS, or HHMMSSD, or HHMMSSDD, where H = hours(00 - 23), M = minutes(00 - 59), S = integer seconds(00 - 59) and DD = decimal seconds; decimal seconds are expressed as follows: D = tenths(0 - 9) and DD = hundredths(00 - 99)
            gs.Elements.Add(new EDIElement() { Position = 6, ID = "28", Name = "Group Control Number" });
            gs.Elements.Add(new EDIElement() { Position = 7, ID = "455", Name = "Responsible Agency Code" });
            gs.Elements.Add(new EDIElement() { Position = 8, ID = "480", Name = "Version ID" });
            Segments.Add(gs);

            EDISegment st = new EDISegment() { ID = "ST", Name = "Transaction Set Header" };
            st.Elements.Add(new EDIElement() { Position = 1, ID = "143", Name = "Transaction Set Identifier Code" });
            st.Elements.Add(new EDIElement() { Position = 2, ID = "329", Name = "Transaction Set Control Number" });
            Segments.Add(st);

            EDISegment xq = new EDISegment() { ID = "XQ", Name = "Reporting Date/Action" };
            xq.Elements.Add(new EDIElement() { Position = 1, ID = "305", Name = "Transaction Handling Code" });
            xq.Elements.Add(new EDIElement() { Position = 2, ID = "373", Name = "Date" }); //This field will be the beginning date of data file date range. Formatting Notes: CCYYMMDD
            xq.Elements.Add(new EDIElement() { Position = 3, ID = "373", Name = "Date" }); //This field will be the ending date of data file date range. Formatting Notes: CCYYMMDD
            Segments.Add(xq);

            EDILoop loop_n1_1 = new EDILoop() { ID = "LOOP_N1_1", Name = "Loop Name" };
            EDISegment loop_n1_n1 = new EDISegment() { ID = "N1", Name = "Name" };
            loop_n1_n1.Elements.Add(new EDIElement() { Position = 1, ID = "98", Name = "Entity Identifier Code" });
            loop_n1_n1.Elements.Add(new EDIElement() { Position = 2, ID = "93", Name = "Name" });
            loop_n1_n1.Elements.Add(new EDIElement() { Position = 3, ID = "66", Name = "Identification Code Qualifier" });
            loop_n1_n1.Elements.Add(new EDIElement() { Position = 4, ID = "67", Name = "Identification Code" });
            loop_n1_n1.SegmentUse = EEDISegmentUse.Single;
            loop_n1_1.Children.Add(loop_n1_n1);
            EDISegment loop_n1_dtm = new EDISegment() { ID = "DTM", Name = "Date/Time Reference" };
            loop_n1_dtm.Elements.Add(new EDIElement() { Position = 1, ID = "374", Name = "Date/Time Qualifier" });
            loop_n1_dtm.Elements.Add(new EDIElement() { Position = 2, ID = "373", Name = "Date" });
            loop_n1_dtm.SegmentUse = EEDISegmentUse.Multiple;
            loop_n1_1.Children.Add(loop_n1_dtm);
            Segments.Add(loop_n1_1);

            EDISegment n1 = new EDISegment() { ID = "N1", Name = "Name" };
            n1.Elements.Add(new EDIElement() { Position = 1, ID = "98", Name = "Entity Identifier Code" });
            n1.Elements.Add(new EDIElement() { Position = 2, ID = "93", Name = "Name" });
            n1.Elements.Add(new EDIElement() { Position = 3, ID = "66", Name = "Identification Code Qualifier" });
            n1.Elements.Add(new EDIElement() { Position = 4, ID = "67", Name = "Identification Code" });
            Segments.Add(n1);

            EDILoop loop_lin = new EDILoop() { ID = "LOOP_LIN", Name = "Loop Item Identification" };
            EDISegment loop_lin_lin = new EDISegment() { ID = "LIN", Name = "Item Identification" };
            loop_lin_lin.Elements.Add(new EDIElement() { Position = 1, ID = "350", Name = "Assigned Identification" });
            loop_lin_lin.Elements.Add(new EDIElement() { Position = 2, ID = "235", Name = "Product/Service ID Qualifier" }); //BP Buyer's Part Number; EN European Article Number(EAN) (2 - 5 - 5 - 1); UP U.P.C.Consumer Package Code(1 - 5 - 5 - 1); VN Vendor's (Seller's) Item Number
            loop_lin_lin.Elements.Add(new EDIElement() { Position = 3, ID = "234", Name = "Product/Service ID" }); //This field will reflect the first article identfier number you require
            loop_lin_lin.Elements.Add(new EDIElement() { Position = 4, ID = "235", Name = "Product/Service ID Qualifier" }); //BP Buyer's Part Number; EN European Article Number(EAN) (2 - 5 - 5 - 1); UP U.P.C.Consumer Package Code(1 - 5 - 5 - 1); VN Vendor's (Seller's) Item Number
            loop_lin_lin.Elements.Add(new EDIElement() { Position = 5, ID = "234", Name = "Product/Service ID" }); //This field will reflect the secondary article identfier number you require
            loop_lin_lin.SegmentUse = EEDISegmentUse.Single;
            loop_lin.Children.Add(loop_lin_lin);

            EDISegment loop_lin_n9 = new EDISegment() { ID = "N9", Name = "Reference Identification" };
            loop_lin_n9.Elements.Add(new EDIElement() { Position = 1, ID = "128", Name = "Reference Identification Qualifier" });
            loop_lin_n9.Elements.Add(new EDIElement() { Position = 2, ID = "127", Name = "Reference Identification" });
            loop_lin_n9.SegmentUse = EEDISegmentUse.Single;
            loop_lin.Children.Add(loop_lin_n9);

            EDISegment loop_lin_amt = new EDISegment() { ID = "AMT", Name = "Monetary Amount" };
            loop_lin_amt.Elements.Add(new EDIElement() { Position = 1, ID = "522", Name = "Amount Qualifier Code" }); //4T Cost of Goods Sold(Src Vendor); 8T Cost of Goods Sold(Mfg Vendor)
            loop_lin_amt.Elements.Add(new EDIElement() { Position = 2, ID = "782", Name = "Monetary Amount" }); //Amount
            loop_lin_amt.SegmentUse = EEDISegmentUse.Single;
            loop_lin.Children.Add(loop_lin_amt);

            EDILoop loop_lin_loop_za = new EDILoop() { ID = "LOOP_ZA", Name = "Loop Product Activity Reporting QA" };
            EDISegment loop_lin_loop_za_za = new EDISegment() { ID = "ZA", Name = "Product Activity Reporting" };
            loop_lin_loop_za_za.Elements.Add(new EDIElement() { Position = 1, ID = "859", Name = "Activity Code" });
            /*
             DG Quantity Damaged; Description: Total product listed as damaged goods
DS Days Supply; Description: This is a predictive indicator that calculates the Average Units Sold within a couple weeks and then using that figure, calculates the inventory dayss supply on-hand.
FV Forecast Variance; Description: The variance between actual sales and the forecasted sales
HL Quantity on Hold; Description: The quantity of this catalog number on reserve. (The reserve is usually a commitment by the customer to hold a specified amount of stock.)
QA Current Inventory Quantity Available for Shipment or Sale; Description: Indicates the quantity currently available to be sold or shipped
QE Ending Balance Quantity; Description: Closing inventory quantity for the current inventory reporting period
QI Quantity in Transit; Description: Quantity of stock replenishment shipped but not received
QO Quantity Out of Stock; Description: The quantity of this item currently back ordered to the end customer.
QP Quantity On Order, Not Yet Received; Description: Total quantity expected to be received from supplier for current reporting period, but not yet received
QR Quantity Received; Description: The quantity received into the buying party's system since the last 852 product activity data update.
QS Quantity Sold; Description: The quantity sold from the buying party's system since the last 852 product activity data update.
QU Quantity Returned By Consumer; Description: Qty returned by consumer
ST Sell Thru Percentage; Description: Sell-thru is a composite measure that shows sales and inventory in one metric. Sell-thru is very useful as a filter for top selling items.
TS Total Sales Quantity; Description: Total sales of product
WS Weeks Supply; Description: This is a predictive indicator that calculates the Average Units Sold within a couple weeks and then using that figure, calculates the inventory weeks supply on-hand 
             */
            loop_lin_loop_za_za.SegmentUse = EEDISegmentUse.Single;
            loop_lin_loop_za.Children.Add(loop_lin_loop_za_za);
            EDISegment loop_lin_loop_za_sdq = new EDISegment() { ID = "SDQ", Name = "Destination Quantity" };
            loop_lin_loop_za_sdq.Elements.Add(new EDIElement() { Position = 1, ID = "355", Name = "Unit or Basis for Measurement Code" });
            loop_lin_loop_za_sdq.Elements.Add(new EDIElement() { Position = 2, ID = "66", Name = "Identification Code Qualifier" });
            loop_lin_loop_za_sdq.Elements.Add(new EDIElement() { Position = 3, ID = "67", Name = "Identification Code" });
            loop_lin_loop_za_sdq.Elements.Add(new EDIElement() { Position = 4, ID = "380", Name = "Quantity" });
            loop_lin_loop_za_sdq.SegmentUse = EEDISegmentUse.Single;
            loop_lin_loop_za.Children.Add(loop_lin_loop_za_sdq);

            loop_lin_loop_za.SegmentUse = EEDISegmentUse.Multiple;
            loop_lin.Children.Add(loop_lin_loop_za);

            Segments.Add(loop_lin);

            EDISegment ctt = new EDISegment() { ID = "CTT", Name = "Transaction Totals" };
            ctt.Elements.Add(new EDIElement() { Position = 1, ID = "354", Name = "Number of Line Items" });
            Segments.Add(ctt);

            EDISegment se = new EDISegment() { ID = "SE", Name = "Transaction Set Trailer" };
            se.Elements.Add(new EDIElement() { Position = 1, ID = "96", Name = "Number of Included Segments" });
            se.Elements.Add(new EDIElement() { Position = 2, ID = "329", Name = "Transaction Set Control Number" });
            Segments.Add(se);

            EDISegment ge = new EDISegment() { ID = "GE", Name = "Functional Group Trailer" };
            ge.Elements.Add(new EDIElement() { Position = 1, ID = "97", Name = "Number of Transaction Sets Included" });
            ge.Elements.Add(new EDIElement() { Position = 2, ID = "28", Name = "Group Control Number" });
            Segments.Add(ge);

            EDISegment iea = new EDISegment() { ID = "IEA", Name = "Interchange Control Trailer" };
            iea.Elements.Add(new EDIElement() { Position = 1, ID = "I16", Name = "Number of Included Functional Groups" });
            iea.Elements.Add(new EDIElement() { Position = 2, ID = "I12", Name = "Interchange Control Number" });
            Segments.Add(iea);

        }





    }
}



