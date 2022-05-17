using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.tool.xml;
using iTextSharp.tool.xml.html;
using iTextSharp.tool.xml.parser;
using iTextSharp.tool.xml.pipeline.css;
using iTextSharp.tool.xml.pipeline.end;
using iTextSharp.tool.xml.pipeline.html;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace GetCotizacionPagoPendiente
{
  class Program
  {
    static void Main(string[] args)
    {
      var inicio = DateTime.Now;
      Console.WriteLine("inicio: {0}", DateTime.Now.ToString());
      //////////obtener lista de transacciones//////////////////////////
      List<Transacciones> totalTransactions = GetListOfOpenTransactions();
      //////////////////////////////////////////////////////////////////////

      String template = String.Empty;
      //template += "<!DOCTYPE html>";
      //template += "<html lang=\"en\">";
      //template += "<head><title>Title</title></head><body>";
      foreach (Transacciones trans in totalTransactions)
      //Parallel.ForEach(totalTransactions, (trans) =>
      {
        ConsultaEntity entity = new ConsultaEntity();
        String url = "https://ayt.operations.dynamics.com/Data/SalesQuotationHeaders?$filter=RequestingCustomerAccountNumber eq '" + trans.ref_cliente + "' and (SalesQuotationStatus ne Microsoft.Dynamics.DataEntities.SalesQuotationStatus'Lost' and SalesQuotationStatus ne Microsoft.Dynamics.DataEntities.SalesQuotationStatus'Cancelled') and RequestedShippingDate gt " + DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd") + "T12:00:00Z&$expand=SalesQuotationLine($select=LineAmount,ItemNumber,SalesTaxGroupCode)&$select=SalesQuotationNumber,SalesQuotationStatus,RequestedShippingDate,DeliveryModeCode";
        var result = entity.QueryEntity(url);
        SalesQuotationResult resultObj = JsonConvert.DeserializeObject<SalesQuotationResult>(result.Result.Content);
        var salesQuotationsWithAmount = GetSalesQuotationsWithAmount(resultObj, trans.monto);
        template += GetHtmlTemplate(salesQuotationsWithAmount, trans);
      }
      //);
      //template += "</body></html>";
      StringReader sr = new StringReader(template);
      using (MemoryStream ms2 = new MemoryStream())
      {
        Document document = new Document(PageSize.LETTER, 15, 15, 15, 15);
        PdfWriter writer2 = PdfWriter.GetInstance(document, ms2);
        document.Open();
        HtmlPipelineContext htmlContext = new HtmlPipelineContext(new CssAppliersImpl());
        htmlContext.SetTagFactory(Tags.GetHtmlTagProcessorFactory());
        ICSSResolver cssResolver = XMLWorkerHelper.GetInstance().GetDefaultCssResolver(true);
        IPipeline pipeline = new CssResolverPipeline(cssResolver, new HtmlPipeline(htmlContext, new PdfWriterPipeline(document, writer2)));
        XMLWorker worker = new XMLWorker(pipeline, true);
        XMLParser p = new XMLParser(true, worker, Encoding.UTF8);
        try
        {
          p.Parse(stringToStream(template));
        }
        catch (Exception e)
        {
          Console.WriteLine(e.Message);
        }
        document.Close();
        writer2.Close();
        Mail correo = new Mail();
        correo.SendMail(template,ms2);
        Console.WriteLine("fin: {0}", DateTime.Now.ToString());
        var fin = DateTime.Now;
        Console.WriteLine("duracion: {0}", Math.Round(((fin - inicio).TotalSeconds), 2));
      }
    }

    static List<SalesQuotationAmountResult> GetSalesQuotationsWithAmount(SalesQuotationResult data, Double transMonto)
    {
      List<SalesQuotationAmountResult> result = new List<SalesQuotationAmountResult>();
      foreach(var value in data.value)
      {
        SalesQuotationAmountResult salesQuotation = new SalesQuotationAmountResult();
        salesQuotation.SalesQuotationNumber = value.SalesQuotationNumber;
        salesQuotation.RequestedShippingDate = Convert.ToDateTime(value.RequestedShippingDate).ToString("dd/MM/yyyy");
        salesQuotation.DeliveryModeCode = value.DeliveryModeCode;
        if (value.SalesQuotationStatus != "Confirmed")
        {
          salesQuotation.Amount = Math.Truncate( value.SalesQuotationLine.Sum(quot => quot.LineAmount * ((quot.SalesTaxGroupCode == "VTAS") ? 1.16 : 1.08)) * 100 ) / 100;
          result.Add(salesQuotation);
          Console.ForegroundColor = ConsoleColor.Gray;
          if (salesQuotation.Amount >= (transMonto - 1) && salesQuotation.Amount <= (transMonto + 1))
          {
            Console.ForegroundColor = ConsoleColor.Red;
          }
          Console.WriteLine("Cotizacion: {0} Monto: {1}", value.SalesQuotationNumber, salesQuotation.Amount);
          Console.ForegroundColor = ConsoleColor.Gray;
        }
      }
      return result;
    }

    static string GetHtmlTemplate(List<SalesQuotationAmountResult> data,Transacciones trans)
    {
      
        String html = String.Empty;
        html += "<table style=\"border-collapse:collapse; font-size: 11px; margin: 20px !important; page-break-inside:avoid;\">" +
                 "   <tbody>" +
                 "     <tr style=\"font-weight:bolder;width: 90%;\">" +
                 "        <td style=\"border: solid 1px black; color:white; background-color: #2874A6; width:10%;\">Cliente</td>" +
                 "        <td style=\"border: solid 1px black; color:white; background-color: #2874A6; width:31%;\" >Nombre</td>" +
                 "        <td style=\"border: solid 1px black; color:white; background-color: #2874A6; width:13%;\">Tipo Cliente</td>" +
                 "        <td style=\"border: solid 1px black; color:white; background-color: #2874A6; width:13%;\">Zona de Ventas</td>" +
                 "        <td style=\"border: solid 1px black; color:white; background-color: #2874A6; width:20%;\">Fecha de Pago</td>" +
                 "        <td style=\"border: solid 1px black; color:white; background-color: #2874A6; width:13%;\">Monto de pago</td>" +
                 "     </tr>" +
                 "     <tr>" +
                 "        <td style=\"border: solid 1px black; font-size: 10px;\">" + trans.ref_cliente + "</td>" +
                 "        <td style=\"border: solid 1px black; font-size: 10px;\">" + trans.nombre_cliente + "</td>" +
                 "        <td style=\"border: solid 1px black; font-size: 10px;\">" + trans.tipo_cliente + "</td>" +
                 "        <td style=\"border: solid 1px black; font-size: 10px;\">" + trans.zona_ventas + "</td>" +
                 "        <td style=\"border: solid 1px black; font-size: 10px;\">" + trans.fecha_pago + "</td>" +
                 "        <td style=\"border: solid 1px black; font-weight:bolder; font-size: 10px;\">" + String.Format("{0:C}", trans.monto) + "</td>" +
                 "     </tr>" +
                 "     <tr>" +
                 "        <td>&nbsp;</td>" +
                 "        <td style=\"color:white; background-color: #5DADE2; border: solid 1px black; font-weight:bolder;\">Fecha</td>" +
                 "        <td style=\"color:white; background-color: #5DADE2; border: solid 1px black; font-weight:bolder;\">Cotizacion</td>" +
                 "        <td style=\"color:white; background-color: #5DADE2; border: solid 1px black; font-weight:bolder;\">Modo de entrega</td>" +
                 "        <td style=\"color:white; background-color: #5DADE2; border: solid 1px black; font-weight:bolder;\">Monto</td>" +
                 "        <td>&nbsp;</td>" +
                 "     </tr>";
      if (data.Count == 0)
      {
        html += "   <tr>" +
                "     <td>&nbsp;</td>" +
                "     <td style=\"border: solid 1px black; color: black; text-align:center; font-weight:bolder;\" colspan=\"4\"> No hay Cotizaciones para este pago </td>" +
                "     <td>&nbsp;</td>" +
                "   </tr>";
      }
      foreach (var salesQuotRes in data)
        {
          String color = "black;";
          String font_weight = "";
          if (salesQuotRes.Amount >= (trans.monto - 1) && salesQuotRes.Amount <= (trans.monto + 1))
          {
            color = "red; ";
            font_weight = "font-weight:bolder;";
          }
          html += "   <tr>" +
                  "     <td>&nbsp;</td>" +
                  "     <td style=\"border: solid 1px black; color: " + color + " font-size: 10px;\">" + salesQuotRes.RequestedShippingDate + "</td>" +
                  "     <td style=\"border: solid 1px black; color: " + color + " font-size: 10px;\">" + salesQuotRes.SalesQuotationNumber + "</td>" +
                  "     <td style=\"border: solid 1px black; color: " + color + " font-size: 10px;\">" + HttpUtility.HtmlEncode(salesQuotRes.DeliveryModeCode) + "</td>" +
                  "     <td style=\"border: solid 1px black; color: " + color + font_weight + " font-size: 10px;\">" + String.Format("{0:C}", salesQuotRes.Amount) + "</td>" +
                  "     <td>&nbsp;</td>" +
                  "   </tr>";
        }
        html += "     <tr><td colspan=\"6\">&nbsp;</td></tr>";
        html += "</tbody></table>";
        return html;
      /*}
      else
      {
        return "";
      }*/
    }
    static List<Transacciones> GetListOfOpenTransactions()
    {
      List<Transacciones> transacciones = new List<Transacciones>();
      databaseAyt03 ayt03 = new databaseAyt03();
      String QueryInvoices = "SELECT ref_cliente,nombre_cliente,tipo_cliente,zona_ventas,fecha_pago,monto FROM [dbo].[transacciones] WHERE asignado = 0 AND descartar = 0 AND tipo_cliente = 'CONTADO' AND tipo_cobro != 'DEP S B COBRO' ORDER BY fecha_pago;";
      SqlDataReader reader = (SqlDataReader)ayt03.query(QueryInvoices);
      if (reader.HasRows)
      {
        while (reader.Read())
        {
          Transacciones simpleTrans = new Transacciones();
          simpleTrans.ref_cliente = reader[0].ToString();
          simpleTrans.nombre_cliente = reader[1].ToString();
          simpleTrans.tipo_cliente = reader[2].ToString();
          simpleTrans.zona_ventas = reader[3].ToString();
          simpleTrans.fecha_pago = reader[4].ToString();
          simpleTrans.monto = Convert.ToDouble(reader[5].ToString());
          transacciones.Add(simpleTrans);
        }
      }
      else
      {
        Console.WriteLine("No rows found.");
      }
      reader.Close();
      return transacciones;
    }
    static Stream stringToStream(String txt)
    {
      var stream = new MemoryStream();
      var w = new StreamWriter(stream);
      w.Write(txt);
      w.Flush();
      stream.Position = 0;
      return stream;
    }
  }

  public class SalesQuotationAmountResult 
  {
    public double Amount { get; set; }
    public String SalesQuotationNumber { get; set; }
    public String RequestedShippingDate { get; set; }
    public String DeliveryModeCode { get; set; }
  }

  public class SalesQuotationResult
  {
    public List<SalesQuotationResultValue> value { get; set; }    
  }

  public class SalesQuotationResultValue
  {
    public String SalesQuotationNumber { get; set; }
    public String SalesQuotationStatus { get; set; }
    public String RequestedShippingDate { get; set; }
    public String DeliveryModeCode { get; set; }
    public List<SalesQuotationLines> SalesQuotationLine { get; set; }
  }

  public class SalesQuotationLines
  {
    public double LineAmount { get; set; }
    public String ItemNumber { get; set; }
    public String SalesTaxGroupCode { get; set; }
  }

  public class Transacciones
  {
    public String ref_cliente { get; set; }
    public String nombre_cliente { get; set; }
    public String tipo_cliente { get; set; }
    public String zona_ventas { get; set; }
    public String fecha_pago { get; set; }
    public Double monto { get; set; }
  }
}
