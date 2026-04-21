export function exportInvoicePdf(inv) {
  const { jsPDF } = window.jspdf;
  const doc = new jsPDF({ unit: 'mm', format: 'letter' });

  const lm   = 15;   // left margin
  const rm   = 200;  // right content edge
  const fmt  = n => '$' + Number(n).toLocaleString('en-US', { minimumFractionDigits: 2 });
  const total = Number(inv.line_haul_rate) + Number(inv.fsc_rate);

  // ── Company header ──────────────────────────────────────────
  doc.setFontSize(9);
  doc.setFont('helvetica', 'normal');
  doc.text('ABC Trucking Company', lm, 18);
  doc.text('1234 Road Ln Haleyville,', lm, 24);
  doc.text('AL 35565', lm, 29);

  // ── "Invoice" title ─────────────────────────────────────────
  doc.setFontSize(28);
  doc.setFont('helvetica', 'bold');
  doc.text('Invoice', rm, 20, { align: 'right' });

  // ── Date / Invoice # box ────────────────────────────────────
  doc.setFontSize(9);
  doc.setFont('helvetica', 'normal');
  doc.setLineWidth(0.3);
  const bxL = 133, bxT = 33, bxW = 67, bxH = 18, bxMid = bxL + bxW / 2;
  doc.rect(bxL, bxT, bxW, bxH);
  doc.line(bxMid, bxT, bxMid, bxT + bxH);
  doc.line(bxL, bxT + 8, bxL + bxW, bxT + 8);
  doc.text('Date',      bxL  + bxW * 0.25, bxT + 5.5, { align: 'center' });
  doc.text('Invoice #', bxL  + bxW * 0.75, bxT + 5.5, { align: 'center' });
  doc.text(inv.invoice_date,              bxL + bxW * 0.25, bxT + 14, { align: 'center' });
  doc.text(String(inv.invoice_number),    bxL + bxW * 0.75, bxT + 14, { align: 'center' });

  // ── Bill To / Consignee boxes ───────────────────────────────
  const bbt = 55, bbH = 45, bbW = 90;
  const cxL = lm + bbW + 5;

  doc.rect(lm,  bbt, bbW, bbH);
  doc.rect(cxL, bbt, bbW, bbH);
  doc.line(lm,  bbt + 9, lm  + bbW, bbt + 9);
  doc.line(cxL, bbt + 9, cxL + bbW, bbt + 9);

  doc.setFont('helvetica', 'normal');
  doc.text('Bill To',   lm  + 4, bbt + 6);
  doc.text('Consignee', cxL + 4, bbt + 6);

  doc.text(inv.bill_to_name, lm + 4, bbt + 15);
  const billAddrLines = doc.splitTextToSize(inv.bill_to_address, bbW - 8);
  doc.text(billAddrLines, lm + 4, bbt + 21);

  doc.text(inv.consignee_name, cxL + 4, bbt + 15);
  const consAddrLines = doc.splitTextToSize(inv.consignee_address, bbW - 8);
  doc.text(consAddrLines, cxL + 4, bbt + 21);

  // ── Load info table ─────────────────────────────────────────
  const ltL = 58, ltT = 107, ltR = rm, ltW = ltR - ltL, ltH = 16;
  const ltCols = [ltL, ltL + 35, ltL + 71, ltL + 107, ltR];

  doc.rect(ltL, ltT, ltW, ltH);
  doc.line(ltL, ltT + 8, ltR, ltT + 8);
  for (let i = 1; i < 4; i++)
    doc.line(ltCols[i], ltT, ltCols[i], ltT + ltH);

  const ltHeaders = ['Load No.', 'Terms', 'Ship Date', 'Unit No.'];
  const ltValues  = [inv.load_number, inv.terms, inv.ship_date, inv.unit_number ?? '—'];
  for (let i = 0; i < 4; i++) {
    const cx = (ltCols[i] + ltCols[i + 1]) / 2;
    doc.text(ltHeaders[i], cx, ltT + 5.5, { align: 'center' });
    doc.text(String(ltValues[i]), cx, ltT + 13,  { align: 'center' });
  }

  // ── Description table ───────────────────────────────────────
  const dtT     = ltT + ltH;
  const dtHdrH  = 8;
  const dtBodyH = 110;
  const dc = [lm, 112, 148, 174, rm];

  // Header row (grey fill)
  doc.setFillColor(220, 220, 220);
  doc.rect(lm, dtT, rm - lm, dtHdrH, 'FD');
  for (let i = 1; i < 4; i++)
    doc.line(dc[i], dtT, dc[i], dtT + dtHdrH);

  doc.setFont('helvetica', 'bold');
  const dtHeaders = ['Description', 'Quantity', 'Rate', 'Amount'];
  for (let i = 0; i < 4; i++)
    doc.text(dtHeaders[i], (dc[i] + dc[i + 1]) / 2, dtT + 5.5, { align: 'center' });

  // Body
  doc.setFont('helvetica', 'normal');
  doc.rect(lm, dtT + dtHdrH, rm - lm, dtBodyH);
  for (let i = 1; i < 4; i++)
    doc.line(dc[i], dtT + dtHdrH, dc[i], dtT + dtHdrH + dtBodyH);

  let rowY = dtT + dtHdrH + 6;

  // Line haul row
  doc.text(inv.description,                  lm + 2,     rowY);
  doc.text(fmt(inv.line_haul_rate), (dc[2] + dc[3]) / 2, rowY, { align: 'center' });
  doc.text(fmt(inv.line_haul_rate),           rm - 2,     rowY, { align: 'right' });
  rowY += 6;

  // FSC row (if applicable)
  if (Number(inv.fsc_rate) > 0) {
    doc.text('Fuel Surcharge (FSC)',           lm + 2,     rowY);
    doc.text(fmt(inv.fsc_rate), (dc[2] + dc[3]) / 2,      rowY, { align: 'center' });
    doc.text(fmt(inv.fsc_rate),                rm - 2,     rowY, { align: 'right' });
  }

  // ── Footer ──────────────────────────────────────────────────
  const ftT = dtT + dtHdrH + dtBodyH;
  const ftH = 10;
  doc.rect(lm, ftT, rm - lm, ftH);
  doc.line(dc[2], ftT, dc[2], ftT + ftH);

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(9);
  doc.text('Rate Confirmation attached', lm + 2, ftT + 6.5);

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(11);
  doc.text('Total', (dc[2] + rm) / 2, ftT + 6.5, { align: 'center' });

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(9);
  doc.text(fmt(total), rm - 2, ftT + 6.5, { align: 'right' });

  doc.save(`Invoice-${inv.invoice_number}.pdf`);
}
