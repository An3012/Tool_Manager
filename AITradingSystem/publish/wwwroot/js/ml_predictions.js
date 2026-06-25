async function loadPredictions(runId){
    try{
        const res = await fetch('/api/ml/predictions/history?runId='+encodeURIComponent(runId));
        if(!res.ok){ showToast('Không lấy được dự đoán', 'danger'); return; }
        const items = await res.json();
        const modal = document.getElementById('predModal');
        const body = document.getElementById('predModalBody');
        body.innerHTML = '';
        if(items.length===0){ body.textContent = 'Không có dự đoán'; }
        else{
            items.forEach(it=>{
                const div = document.createElement('div');
                div.className = 'mb-2 p-2 border rounded';
                div.innerHTML = `<div><strong>${new Date(it.predictedAt).toLocaleString()}</strong> → ${it.action || '-'} (conf: ${it.confidence || 0})</div><div style="font-size:12px;color:#ddd">${it.details || ''}</div>`;
                body.appendChild(div);
            });
        }
        modal.style.display = 'block';
    }catch(e){ showToast('Lỗi tải dự đoán', 'danger'); }
}

function closePredModal(){ document.getElementById('predModal').style.display='none'; }
