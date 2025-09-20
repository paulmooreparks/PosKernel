// Store-Level Void Operations
// Add these methods to the LegalKernelStore impl block in pos-kernel-rs/src/lib.rs

impl LegalKernelStore {
    // NEW: Void line item operation
    fn void_line_item_legal(&mut self, handle: u64, line_number: u32, reason: String, operator_id: Option<String>) -> Result<(), Box<dyn std::error::Error>> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        match tx.state {
            LegalTxState::Building => {
                if tx.is_expired(self.system_config.transaction_timeout_seconds) {
                    return Err("Transaction expired".into());
                }
                
                tx.void_line_item(line_number, reason.clone(), operator_id.clone())?;
                tx.last_activity = SystemTime::now();
                
                // WAL logging for audit compliance
                if let Err(e) = self.log_wal_entry("VOID_LINE", handle, &format!("line:{}, reason:{}", line_number, reason)) {
                    eprintln!("WARNING: Failed to log void operation: {}", e);
                    // Continue - the void was successful, logging failure shouldn't prevent operation
                }
                
                Ok(())
            },
            _ => Err("Transaction not in building state".into())
        }
    }
    
    // NEW: Update line item quantity operation
    fn update_line_quantity_legal(&mut self, handle: u64, line_number: u32, new_quantity: i32, operator_id: Option<String>) -> Result<(), Box<dyn std::error::Error>> {
        let tx = self.active_transactions.get_mut(&handle)
            .ok_or("Transaction not found")?;
        
        match tx.state {
            LegalTxState::Building => {
                if tx.is_expired(self.system_config.transaction_timeout_seconds) {
                    return Err("Transaction expired".into());
                }
                
                tx.update_line_quantity(line_number, new_quantity, operator_id.clone())?;
                tx.last_activity = SystemTime::now();
                
                // WAL logging for audit compliance
                if let Err(e) = self.log_wal_entry("UPDATE_QTY", handle, &format!("line:{}, qty:{}", line_number, new_quantity)) {
                    eprintln!("WARNING: Failed to log quantity update: {}", e);
                    // Continue - the update was successful, logging failure shouldn't prevent operation
                }
                
                Ok(())
            },
            _ => Err("Transaction not in building state".into())
        }
    }
}
