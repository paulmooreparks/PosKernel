// Transaction Methods for Void Operations
// Add these methods to the LegalTransaction impl block in pos-kernel-rs/src/lib.rs

impl LegalTransaction {
    // Update the existing add_line method (around line 603) to assign proper line numbers
    fn add_line(&mut self, sku: String, qty: i32, unit_minor: i64) {
        let line_number = self.next_line_number();
        self.lines.push(Line::new_sale(sku, qty, unit_minor, line_number, None));
        self._recovery_point += 1;
    }
    
    // NEW: Void line item by line number
    fn void_line_item(&mut self, line_number: u32, reason: String, operator_id: Option<String>) -> Result<(), String> {
        // Find original line item
        let original_line = self.lines.iter()
            .find(|line| line.line_number == line_number && line.entry_type == EntryType::Sale)
            .ok_or("Line item not found or not a sale item")?
            .clone();
        
        // Check if already voided
        let already_voided = self.lines.iter()
            .any(|line| line.entry_type == EntryType::Void && line.references_line == Some(line_number));
        
        if already_voided {
            return Err("Line item already voided".to_string());
        }
        
        // Create reversing entry
        let void_line_number = self.next_line_number();
        let void_entry = Line::new_void(&original_line, void_line_number, reason, operator_id);
        
        self.lines.push(void_entry);
        self._recovery_point += 1;
        Ok(())
    }
    
    // NEW: Update line item quantity
    fn update_line_quantity(&mut self, line_number: u32, new_quantity: i32, operator_id: Option<String>) -> Result<(), String> {
        if new_quantity <= 0 {
            return Err("Use void_line_item for removing items completely".to_string());
        }
        
        // Find original line item
        let original_line = self.lines.iter()
            .find(|line| line.line_number == line_number && line.entry_type == EntryType::Sale)
            .ok_or("Line item not found")?;
        
        // Calculate effective quantity including any previous adjustments
        let effective_qty = self.calculate_effective_quantity_for_line(line_number);
        let qty_diff = new_quantity - effective_qty;
        
        if qty_diff != 0 {
            let adjustment_line_number = self.next_line_number();
            let adjustment_entry = Line {
                sku: original_line.sku.clone(),
                qty: qty_diff,
                unit_minor: original_line.unit_minor,
                line_number: adjustment_line_number,
                entry_type: EntryType::Adjustment,
                void_reason: Some(format!("Quantity changed from {} to {}", effective_qty, new_quantity)),
                references_line: Some(line_number),
                timestamp: SystemTime::now(),
                operator_id,
            };
            
            self.lines.push(adjustment_entry);
            self._recovery_point += 1;
        }
        
        Ok(())
    }
    
    // Helper: Get next line number
    fn next_line_number(&self) -> u32 {
        self.lines.len() as u32 + 1
    }
    
    // Helper: Calculate effective quantity for a specific line (considering adjustments)
    fn calculate_effective_quantity_for_line(&self, line_number: u32) -> i32 {
        self.lines.iter()
            .filter(|line| line.line_number == line_number || line.references_line == Some(line_number))
            .map(|line| line.qty)
            .sum()
    }
    
    // Helper: Calculate total considering all entries (voids, adjustments)
    fn calculate_effective_total(&self) -> i64 {
        self.lines.iter()
            .map(|line| (line.qty as i64) * line.unit_minor)
            .sum()
    }
}
