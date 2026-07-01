# Improvement Plan – Nâng cấp `GenerateGlobalPortfolioPlanAsync()` nhưng vẫn giữ nguyên kiến trúc hiện tại

## Mục tiêu

Nâng cấp khả năng lập kế hoạch của AI trong `GenerateGlobalPortfolioPlanAsync()` mà **không thay đổi kiến trúc tổng thể hiện tại**.

Hệ thống vẫn giữ nguyên luồng xử lý:

```
Thu thập dữ liệu
        ↓
Xây dựng Prompt
        ↓
LLM phân tích
        ↓
GlobalPortfolioPlanResult
```

Tuy nhiên, Prompt và dữ liệu đầu vào cần được cải tiến để AI có thể lập kế hoạch danh mục ở mức chuyên nghiệp hơn.

---

# Mục tiêu cải tiến

Việc nâng cấp phải giúp AI:

* đánh giá toàn bộ danh mục thay vì từng mã riêng lẻ.
* tối ưu việc sử dụng toàn bộ nguồn vốn.
* giảm tối đa tiền mặt còn dư.
* cân nhắc nhiều phương án phân bổ vốn trước khi đưa ra quyết định.
* chủ động đề xuất xoay vòng vốn khi cần.
* giải thích rõ lý do của từng quyết định.

Không thay đổi cấu trúc JSON hiện tại.

Không thay đổi model `GlobalPortfolioPlanResult`.

---

# Phase 1 – Portfolio-Level Thinking

## Hiện tại

AI thường phân tích theo từng mã.

Ví dụ:

```
VCG → BUY

HPG → HOLD

SSI → BUY
```

Sau đó mới ghép thành kế hoạch.

Điều này khiến AI dễ đưa ra quyết định cục bộ.

---

## Yêu cầu mới

AI phải coi toàn bộ danh mục là một hệ thống.

Trước khi đưa ra bất kỳ khuyến nghị nào, AI phải:

* đánh giá toàn bộ danh mục hiện tại.
* đánh giá mức độ hoàn thành mục tiêu lợi nhuận.
* đánh giá khả năng sử dụng nguồn vốn.
* đánh giá tương quan giữa các vị thế.
* đánh giá mức độ tập trung rủi ro.

Chỉ sau đó mới được quyết định hành động cho từng mã.

---

# Phase 2 – Multi-Scenario Evaluation

Trước khi đề xuất kế hoạch cuối cùng, AI phải tự đánh giá nhiều phương án.

Ví dụ:

```
Portfolio A

Portfolio B

Portfolio C

Portfolio D
```

Mỗi phương án cần được so sánh theo:

* Expected Return
* Probability of Success
* Capital Utilization
* Remaining Cash
* Portfolio Risk
* Diversification
* Liquidity
* Trading Cost

AI phải chọn phương án có hiệu quả tổng thể cao nhất.

Không được chọn phương án đầu tiên tìm thấy.

---

# Phase 3 – Idle Cash Optimization

Đây là thay đổi quan trọng nhất.

Sau mỗi đề xuất MUA hoặc MUA THÊM, AI phải tiếp tục đánh giá số tiền còn lại.

Ví dụ:

```
100.000.000

↓

Mua HPG

↓

Còn 8.500.000

↓

Tiếp tục đánh giá:

- Có thể mua SSI?

- Có thể mua VIX?

- Có thể mua SHS?

↓

Nếu có

↓

Tiếp tục phân bổ

↓

Nếu không

↓

Giải thích rõ lý do giữ tiền.
```

AI không được kết thúc việc lập kế hoạch chỉ vì đã đề xuất một giao dịch.

Tiền còn dư luôn phải được xem là một nguồn lực cần tiếp tục tối ưu.

---

# Phase 4 – Capital Allocation Optimization

Việc phân bổ vốn không được thực hiện độc lập theo từng cổ phiếu.

AI phải tối ưu trên toàn bộ danh mục.

Khi đề xuất nhiều lệnh mua, AI cần cân nhắc:

* Expected Return của từng mã.
* Mức độ rủi ro.
* Tỷ trọng hiện tại.
* Mức độ đa dạng hóa.
* Khả năng hoàn thành mục tiêu lợi nhuận.
* Hiệu quả sử dụng vốn.

Không được tập trung toàn bộ vốn vào một mã nếu việc phân bổ sang nhiều mã giúp tăng hiệu quả danh mục.

---

# Phase 5 – Capital Rotation

AI phải chủ động đánh giá việc xoay vòng vốn.

Ví dụ:

Nếu bán một vị thế hiện tại và chuyển vốn sang cơ hội khác giúp:

* tăng Expected Return,
* tăng Probability of Success,
* hoặc giảm Risk,

thì AI phải ưu tiên đề xuất phương án đó.

Không chỉ đánh giá cơ hội mua mới.

---

# Phase 6 – Dynamic Decision Process

AI phải tuân theo quy trình suy luận sau:

```
Đánh giá danh mục

↓

Đánh giá mục tiêu lợi nhuận

↓

Đánh giá nguồn vốn

↓

Sinh nhiều phương án

↓

So sánh các phương án

↓

Chọn phương án tốt nhất

↓

Kiểm tra tiền còn dư

↓

Nếu còn tiền

↓

Tiếp tục tối ưu

↓

Sinh kế hoạch cuối cùng
```

Không được bỏ qua bước tối ưu nguồn vốn còn lại.

---

# Phase 7 – Decision Priority

AI phải ưu tiên theo đúng thứ tự sau:

Priority 1

Hoàn thành mục tiêu lợi nhuận tổng thể.

Priority 2

Tăng xác suất thành công của toàn danh mục.

Priority 3

Tối đa hóa Expected Return.

Priority 4

Tối đa hóa Capital Utilization.

Priority 5

Giảm tiền mặt còn dư.

Priority 6

Đa dạng hóa danh mục.

Priority 7

Kiểm soát rủi ro.

Nếu có xung đột giữa các tiêu chí, AI phải giải thích rõ tiêu chí nào được ưu tiên.

---

# Phase 8 – Decision Explanation

Mỗi quyết định phải giải thích đầy đủ:

* Vì sao chọn hành động này.
* Vì sao không chọn phương án khác.
* Vì sao chọn đúng số lượng cổ phiếu đó.
* Vì sao không đầu tư phần tiền còn lại (nếu có).
* Quyết định này ảnh hưởng thế nào tới mục tiêu lợi nhuận.
* Quyết định này ảnh hưởng thế nào tới mức độ rủi ro.

Không được chỉ giải thích từng cổ phiếu.

Phải giải thích ở cấp độ toàn danh mục.

---

# Phase 9 – Daily Action Calendar

Daily Calendar không chỉ liệt kê hành động.

Mỗi ngày phải phản ánh kế hoạch tối ưu của toàn danh mục.

Nếu nhiều giao dịch xảy ra trong cùng một ngày:

* xác định thứ tự ưu tiên.
* giải thích vì sao giao dịch đó cần thực hiện trước.
* thể hiện sự phụ thuộc giữa các hành động (ví dụ: bán trước rồi mới mua).

---

# Phase 10 – Prompt Enhancement

Bổ sung vào Prompt các nguyên tắc bắt buộc sau:

* AI phải đánh giá toàn bộ danh mục trước khi đánh giá từng cổ phiếu.
* AI phải tự so sánh nhiều phương án phân bổ vốn.
* AI phải tiếp tục tối ưu số tiền còn lại sau mỗi giao dịch.
* AI chỉ được giữ tiền mặt khi việc đầu tư thêm làm giảm chất lượng danh mục hoặc không còn cơ hội phù hợp.
* AI phải coi việc giảm tiền mặt nhàn rỗi là một mục tiêu tối ưu.
* AI phải xem xét khả năng xoay vòng vốn giữa các vị thế.
* AI phải giải thích vì sao phương án được chọn tốt hơn các phương án còn lại.

---

# Tiêu chí nghiệm thu

Việc nâng cấp được coi là hoàn thành khi AI đáp ứng đầy đủ các tiêu chí sau:

* Không còn lập kế hoạch theo từng mã độc lập.
* Luôn đánh giá toàn bộ danh mục trước khi đưa ra quyết định.
* Luôn đánh giá nhiều phương án đầu tư trước khi chọn phương án cuối cùng.
* Luôn tiếp tục tối ưu số tiền còn lại sau mỗi giao dịch.
* Chỉ giữ tiền mặt khi có lý do hợp lý và được giải thích rõ.
* Có khả năng đề xuất xoay vòng vốn giữa các vị thế.
* Mọi quyết định đều giải thích tác động tới mục tiêu lợi nhuận tổng thể.
* Giữ nguyên cấu trúc `GlobalPortfolioPlanResult`.
* Không thay đổi kiến trúc hiện tại của `GenerateGlobalPortfolioPlanAsync()`.
* Không thay đổi quy trình gọi LLM, chỉ nâng cao chất lượng dữ liệu đầu vào và quy trình suy luận mà LLM phải tuân thủ.
